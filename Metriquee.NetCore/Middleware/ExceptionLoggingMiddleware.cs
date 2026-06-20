using System.Diagnostics;
using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Models;
using Metriquee.NetCore.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Metriquee.NetCore.Middleware;

internal sealed class ExceptionLoggingMiddleware(
    RequestDelegate next,
    IOptions<MetriqueeOptions> options,
    ICollectorSink sink)
{
    public async Task Invoke(HttpContext context)
    {
        var opt = options.Value;
        if (!opt.Exceptions.IsEnabled)
        {
            await next(context);
            return;
        }

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var exTypeName = ex.GetType().FullName ?? ex.GetType().Name;
            if (!opt.Exceptions.ExcludedExceptions.Contains(exTypeName))
                await sink.TrackExceptionAsync(ToExceptionEvent(ex, traceId, context, opt.Exceptions));
            throw;
        }
    }

    private static ExceptionEvent ToExceptionEvent(Exception ex, string traceId, HttpContext ctx,
        ExceptionOptions exOpt)
    {
        return new ExceptionEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            StackTrace = exOpt.IncludeStackTrace
                ? TruncateStackTrace(ex.StackTrace ?? string.Empty, exOpt.MaxStackTraceLines)
                : string.Empty,
            InnerException = ex.InnerException is null ? null : ToInner(ex.InnerException, exOpt),
            TraceId = traceId,
            Path = ctx.Request.Path.HasValue ? ctx.Request.Path.Value : null,
            Method = ctx.Request.Method
        };
    }

    private static InnerExceptionInfo ToInner(Exception ex, ExceptionOptions exOpt)
    {
        return new InnerExceptionInfo
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            StackTrace = exOpt.IncludeStackTrace
                ? TruncateStackTrace(ex.StackTrace ?? string.Empty, exOpt.MaxStackTraceLines)
                : null,
            InnerException = ex.InnerException is null ? null : ToInner(ex.InnerException, exOpt)
        };
    }

    private static string TruncateStackTrace(string stackTrace, int maxLines)
    {
        if (maxLines <= 0 || string.IsNullOrEmpty(stackTrace)) return stackTrace;

        var lines = stackTrace.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length <= maxLines) return stackTrace;

        return string.Join('\n', lines.Take(maxLines)) + $"\n... truncated ({lines.Length - maxLines} more lines)";
    }
}