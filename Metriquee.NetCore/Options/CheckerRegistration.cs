namespace Metriquee.NetCore.Options;

// One registered custom health check: its implementation type, the name shown in the
// dashboard, how often it runs, and whether it is enabled.
internal sealed record CheckerRegistration(Type CheckerType, string Name, int IntervalSeconds, bool IsEnabled);
