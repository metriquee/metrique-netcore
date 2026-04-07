using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Models;
using Microsoft.Extensions.Logging;

namespace Metriquee.NetCore.Sinks;

internal sealed class LoggerCollectorSink(ILogger<LoggerCollectorSink> logger) : ICollectorSink
{
    public ValueTask TrackHttpAsync(HttpEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation("HTTP {Method} {Path} {StatusCode} {DurationMs}ms traceId={TraceId}",
            evt.Method, evt.Path, evt.StatusCode, evt.DurationMs, evt.TraceId);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackExceptionAsync(ExceptionEvent evt, CancellationToken ct = default)
    {
        logger.LogError("EX {Type}: {Message} traceId={TraceId} path={Path} method={Method}",
            evt.Type, evt.Message, evt.TraceId, evt.Path, evt.Method);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackMetricsAsync(MetricsEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation("METRICS cpu={Cpu}% ws={Ws} heap={Heap} rps={Rps}",
            evt.CpuProcessPercent, evt.WorkingSetBytes, evt.ManagedHeapBytes, evt.RequestsPerSecond);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackHealthAsync(HealthEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation("HEALTH {Category} status={Status} entries={Count}",
            evt.Category, evt.Status, evt.Entries.Count);
        return ValueTask.CompletedTask;
    }
}