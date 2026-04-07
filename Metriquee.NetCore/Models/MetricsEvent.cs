namespace Metriquee.NetCore.Models;

internal sealed record MetricsEvent
{
    public required DateTimeOffset Timestamp { get; init; }

    public double? CpuProcessPercent { get; init; }
    public long? WorkingSetBytes { get; init; }
    public long? ManagedHeapBytes { get; init; }

    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    public int ThreadPoolAvailableWorkerThreads { get; init; }
    public int ThreadPoolAvailableIoThreads { get; init; }
    public int ThreadPoolMaxWorkerThreads { get; init; }
    public int ThreadPoolMaxIoThreads { get; init; }

    public double? RequestsPerSecond { get; init; }
}