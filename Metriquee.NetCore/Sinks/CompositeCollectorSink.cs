using Metriquee.NetCore.Abstractions;
using Metriquee.NetCore.Models;

namespace Metriquee.NetCore.Sinks;

internal sealed class CompositeCollectorSink(ICollectorSink[] sinks) : ICollectorSink
{
    public async ValueTask TrackHttpAsync(HttpEvent evt, CancellationToken ct = default)
    {
        foreach (var sink in sinks)
            await sink.TrackHttpAsync(evt, ct);
    }

    public async ValueTask TrackExceptionAsync(ExceptionEvent evt, CancellationToken ct = default)
    {
        foreach (var sink in sinks)
            await sink.TrackExceptionAsync(evt, ct);
    }

    public async ValueTask TrackMetricsAsync(MetricsEvent evt, CancellationToken ct = default)
    {
        foreach (var sink in sinks)
            await sink.TrackMetricsAsync(evt, ct);
    }

    public async ValueTask TrackHealthAsync(HealthEvent evt, CancellationToken ct = default)
    {
        foreach (var sink in sinks)
            await sink.TrackHealthAsync(evt, ct);
    }

    public T? GetSink<T>() where T : class, ICollectorSink
    {
        foreach (var sink in sinks)
            if (sink is T match)
                return match;
        return null;
    }
}