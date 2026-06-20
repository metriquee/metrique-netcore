namespace Metriquee.NetCore.Checks;

// Implement this on your own class to run a custom health check. The package resolves the
// checker from a DI scope and calls CheckHealthAsync on the interval you configure via
// opts.Health.AddChecker<T>(name, intervalSeconds). Constructor injection works, so you can
// depend on scoped services such as a DbContext.
public interface IMetriqueeChecker
{
    Task<MetriqueeCheckResult> CheckHealthAsync(CancellationToken ct = default);
}
