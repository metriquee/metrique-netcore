namespace Metriquee.NetCore.Options;

public sealed record BatchOptions
{
    // Maximum number of items in a single batch
    public int MaxBatchSize { get; init; } = 100;

    // Maximum payload size in megabytes
    public int MaxPayloadSizeMb { get; init; } = 2;

    // Maximum time to wait before flushing a batch
    public int FlushIntervalSeconds { get; init; } = 5;
}