namespace Metriquee.NetCore.Options;

public sealed record SenderOptions
{
    public bool EnableSenderSink { get; set; } = false;

    public bool EnableLoggerSink { get; set; } = true;

    // The API key used for authenticating with the collector service.
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}