using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Internal;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Middleware;

internal sealed class HttpLoggingMiddleware(
    RequestDelegate next,
    IOptions<LogCollectorOptions> options,
    ICollectorSink sink,
    RequestCounters counters)
{
    public async Task Invoke(HttpContext context)
    {
        var opt = options.Value;
        if (!opt.Http.IsEnabled)
        {
            await next(context);
            return;
        }

        var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        if (opt.Http.ExcludedPaths.Any(excluded =>
                requestPath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        counters.Increment();

        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        string? requestBody = null;
        if (opt.Http.ShouldCaptureRequestBody && context.Request.Body.CanRead)
            requestBody = await TryReadRequestBody(context, opt);

        var originalResponseBody = context.Response.Body;
        MemoryStream? responseCapture = null;
        if (opt.Http.ShouldCaptureResponseBody && context.Response.Body.CanWrite)
        {
            responseCapture = new MemoryStream();
            context.Response.Body = new TeeStream(originalResponseBody, responseCapture);
        }

        // OnCompleted fires after the response is fully flushed to the client,
        // including any response written by outer middleware (e.g. DeveloperExceptionPageMiddleware).
        // DO NOT restore context.Response.Body before this — TeeStream must stay active
        // so that error responses written by outer middleware are also captured.
        context.Response.OnCompleted(async () =>
        {
            // Restore the original stream now that everything has been written.
            if (responseCapture is not null)
                context.Response.Body = originalResponseBody;

            stopwatch.Stop();

            string? responseBody = null;
            if (responseCapture is not null)
                responseBody = TryReadResponseBody(responseCapture, opt);

            var httpEvent = new HttpEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Path = requestPath,
                Method = context.Request.Method,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Headers = FilterHeaders(context.Request.Headers, opt),
                RequestSizeBytes = context.Request.ContentLength,
                ResponseSizeBytes = context.Response.ContentLength ?? responseCapture?.Length,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                TraceId = traceId
            };

            await sink.TrackHttpAsync(httpEvent);
        });

        // No finally block restoring the stream — OnCompleted handles it.
        await next(context);
    }

    private static ReadOnlyDictionary<string, string?[]> FilterHeaders(IHeaderDictionary headers,
        LogCollectorOptions opt)
    {
        var dict = new Dictionary<string, string?[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in headers)
            if (opt.Http.SensitiveHeaders.Contains(kv.Key))
                dict[kv.Key] = ["***"];
            else
                dict[kv.Key] = kv.Value.ToArray();

        return dict.AsReadOnly();
    }

    private static async Task<string?> TryReadRequestBody(HttpContext ctx, LogCollectorOptions opt)
    {
        ctx.Request.EnableBuffering(opt.Http.MaxBodyBytes, opt.Http.MaxBodyBytes);
        ctx.Request.Body.Position = 0;

        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        if (body.Length > opt.Http.MaxBodyBytes) body = body[..opt.Http.MaxBodyBytes];
        return MaskJsonFields(body, opt.Http.MaskedFields);
    }

    private static string? TryReadResponseBody(MemoryStream captured, LogCollectorOptions opt)
    {
        if (!opt.Http.ShouldCaptureResponseBody || captured.Length == 0) return null;

        captured.Position = 0;
        using var reader = new StreamReader(captured, Encoding.UTF8,
            false, leaveOpen: true);
        var body = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(body)) return null;
        if (body.Length > opt.Http.MaxBodyBytes) body = body[..opt.Http.MaxBodyBytes];

        return MaskJsonFields(body, opt.Http.MaskedFields);
    }

    private static string MaskJsonFields(string body, HashSet<string> maskedFields)
    {
        if (maskedFields.Count == 0 || string.IsNullOrWhiteSpace(body))
            return body;

        try
        {
            var node = JsonNode.Parse(body);
            if (node is null) return body;
            MaskNode(node, maskedFields);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static void MaskNode(JsonNode node, HashSet<string> maskedFields)
    {
        if (node is JsonObject obj)
        {
            foreach (var prop in obj.ToList())
                if (maskedFields.Contains(prop.Key))
                    obj[prop.Key] = "***";
                else if (prop.Value is not null)
                    MaskNode(prop.Value, maskedFields);
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
                if (item is not null)
                    MaskNode(item, maskedFields);
        }
    }

    private sealed class TeeStream(Stream original, MemoryStream capture) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => original.Length;

        public override long Position
        {
            get => original.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            original.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return original.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            original.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            capture.Write(buffer, offset, count);
            original.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            await capture.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
            await original.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await capture.WriteAsync(buffer, cancellationToken);
            await original.WriteAsync(buffer, cancellationToken);
        }
    }
}