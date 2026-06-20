using Metriquee.NetCore.Checks;

namespace Metriquee.NetCore.Options;

public sealed record HealthOptions
{
    // Indicates whether health checks are enabled
    public bool IsEnabled { get; set; } = true;

    // Interval in seconds between health-status publishes (each checker also runs on its own interval)
    public int IntervalSeconds { get; set; } = 60;

    // The custom health checks registered via AddChecker.
    internal List<CheckerRegistration> Checkers { get; } = [];

    // Register a custom health check. It runs every intervalSeconds and shows up in the
    // dashboard under the given name. Set isEnabled: false to keep it registered but not run.
    public HealthOptions AddChecker<TChecker>(string name, int intervalSeconds, bool isEnabled = true)
        where TChecker : class, IMetriqueeChecker
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Health check name must not be empty.", nameof(name));
        if (intervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be greater than zero.");

        Checkers.Add(new CheckerRegistration(typeof(TChecker), name, intervalSeconds, isEnabled));
        return this;
    }
}
