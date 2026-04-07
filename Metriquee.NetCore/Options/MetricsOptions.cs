namespace Metriquee.NetCore.Options;

public sealed record MetricsOptions
{
    // Indicates whether metrics collection is enabled
    public bool IsEnabled { get; set; } = true;

    // Interval in seconds between metrics collection
    public int IntervalSeconds { get; set; } = 30;
}