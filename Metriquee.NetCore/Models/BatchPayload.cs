namespace Metriquee.NetCore.Models;

internal sealed record BatchPayload
{
    public required string ApiKey { get; init; }
    public required DateTimeOffset FlushedAt { get; init; }

    // Process-wide resource tags (see ResourceOptions). Carried at the batch level since they are
    // constant for the lifetime of the process.
    public string? Environment { get; init; }
    public string? Release { get; init; }
    public string? Host { get; init; }

    public List<HttpEvent> HttpEvents { get; init; } = [];
    public List<ExceptionEvent> ExceptionEvents { get; init; } = [];
    public List<MetricsEvent> MetricsEvents { get; init; } = [];
    public List<HealthEvent> HealthEvents { get; init; } = [];
}