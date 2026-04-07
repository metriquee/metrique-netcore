namespace Metriquee.NetCore.Models;

internal sealed record BatchPayload
{
    public required string ApiKey { get; init; }
    public required DateTimeOffset FlushedAt { get; init; }
    public List<HttpEvent> HttpEvents { get; init; } = [];
    public List<ExceptionEvent> ExceptionEvents { get; init; } = [];
    public List<MetricsEvent> MetricsEvents { get; init; } = [];
    public List<HealthEvent> HealthEvents { get; init; } = [];
}