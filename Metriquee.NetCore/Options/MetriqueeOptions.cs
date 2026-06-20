namespace Metriquee.NetCore.Options;

public sealed record MetriqueeOptions
{
    public HttpOptions Http { get; set; } = new();
    public ExceptionOptions Exceptions { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
    public HealthOptions Health { get; set; } = new();
    public BatchOptions Batch { get; set; } = new();
    public SenderOptions Sender { get; set; } = new();
}