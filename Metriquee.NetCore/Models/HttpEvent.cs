namespace Metriquee.NetCore.Models;

internal sealed record HttpEvent
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Path { get; init; }
    public required string Method { get; init; }
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }

    public required IReadOnlyDictionary<string, string?[]> Headers { get; init; }

    public long? RequestSizeBytes { get; init; }
    public long? ResponseSizeBytes { get; init; }

    public string? RequestBody { get; init; }
    public string? ResponseBody { get; init; }

    public required string TraceId { get; init; }
}