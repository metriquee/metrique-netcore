namespace Metriquee.NetCore.Options;

/// <summary>
///     Process-wide resource tags stamped onto every batch the agent ships, so telemetry can be
///     filtered by deployment environment and release in the dashboard. All three are auto-defaulted
///     in <c>AddMetriquee</c> when left blank (see <c>MetriqueeExtensions</c>): <see cref="Environment" />
///     from <c>IHostEnvironment.EnvironmentName</c>, <see cref="Host" /> from the machine name, and
///     <see cref="Release" /> from the entry assembly's informational version. Set <see cref="Release" />
///     explicitly per deploy for meaningful regression tracking.
/// </summary>
public sealed record ResourceOptions
{
    /// <summary>Deployment environment, e.g. "Production" / "Staging".</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Release/version of the running app, e.g. "1.4.2".</summary>
    public string Release { get; set; } = string.Empty;

    /// <summary>Host / instance identifier (defaults to the machine name).</summary>
    public string Host { get; set; } = string.Empty;
}
