namespace Metriquee.NetCore.Checks;

// The result returned by a custom health check. Mirrors ASP.NET Core's HealthCheckResult:
// a status, an optional human-readable message, an optional exception message, and an
// optional bag of extra data. Use the static helpers to build one.
public sealed record MetriqueeCheckResult
{
    public required MetriqueeHealthStatus Status { get; init; }
    public string? Description { get; init; }
    public string? ExceptionMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? Data { get; init; }

    public static MetriqueeCheckResult Healthy(
        string? description = null,
        IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Status = MetriqueeHealthStatus.Healthy, Description = description, Data = data };

    public static MetriqueeCheckResult Degraded(
        string? description = null,
        IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Status = MetriqueeHealthStatus.Degraded, Description = description, Data = data };

    public static MetriqueeCheckResult Unhealthy(
        string? description = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? data = null) =>
        new()
        {
            Status = MetriqueeHealthStatus.Unhealthy,
            Description = description,
            ExceptionMessage = exception?.Message,
            Data = data
        };
}
