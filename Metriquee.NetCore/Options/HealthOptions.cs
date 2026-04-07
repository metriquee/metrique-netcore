namespace Metriquee.NetCore.Options;

public sealed record HealthOptions
{
    // Indicates whether health checks are enabled
    public bool IsEnabled { get; set; } = true;

    // Interval in seconds between health checks
    public int IntervalSeconds { get; set; } = 60;
}