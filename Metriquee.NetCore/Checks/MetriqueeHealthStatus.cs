namespace Metriquee.NetCore.Checks;

// The status a custom health check can report, ordered least-to-most severe.
public enum MetriqueeHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}
