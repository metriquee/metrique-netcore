namespace Metriquee.NetCore.Models;

internal sealed record ExceptionEvent
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Type { get; init; }
    public required string Message { get; init; }
    public required string StackTrace { get; init; }

    public InnerExceptionInfo? InnerException { get; init; }

    public required string TraceId { get; init; }

    public string? Path { get; init; }
    public string? Method { get; init; }
}

internal sealed record InnerExceptionInfo
{
    public required string Type { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
    public InnerExceptionInfo? InnerException { get; init; }
}