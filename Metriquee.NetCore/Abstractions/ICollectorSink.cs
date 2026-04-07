using Metriquee.NetCore.Models;

namespace Metriquee.NetCore.Abstractions;

internal interface ICollectorSink
{
    ValueTask TrackHttpAsync(HttpEvent evt, CancellationToken ct = default);
    ValueTask TrackExceptionAsync(ExceptionEvent evt, CancellationToken ct = default);
    ValueTask TrackMetricsAsync(MetricsEvent evt, CancellationToken ct = default);
    ValueTask TrackHealthAsync(HealthEvent evt, CancellationToken ct = default);
}