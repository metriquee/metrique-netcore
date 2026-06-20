namespace Metriquee.NetCore.Models;

internal sealed record HealthEvent
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Category { get; init; } // "self" | "dependencies"
    public required string Status { get; init; } // "Healthy" | "Degraded" | "Unhealthy"
    public required IReadOnlyDictionary<string, HealthEntry> Entries { get; init; }
}

internal sealed record HealthEntry
{
    public required string Status { get; init; }
    public string? Description { get; init; }
    public string? ExceptionMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
    public TimeSpan Duration { get; init; }

    // Custom-check metadata: how often the check runs and when it last ran.
    public int? IntervalSeconds { get; init; }
    public DateTimeOffset? LastCheckedAt { get; init; }
}