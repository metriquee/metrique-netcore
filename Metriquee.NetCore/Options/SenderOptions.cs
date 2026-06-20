using Metriquee.NetCore.Internal;

namespace Metriquee.NetCore.Options;

public sealed record SenderOptions
{
    public bool EnableSenderSink { get; set; } = false;

    public bool EnableLoggerSink { get; set; } = true;

    /// <summary>
    ///     Collector endpoint and ingest key combined into one value:
    ///     <c>scheme://&lt;apiKey&gt;@host[:port][/path]</c>
    ///     (e.g. <c>https://mq_live_abc123@collector.example.com</c>). This is the single value the
    ///     Metriquee dashboard hands you when you register an app — it is the only sender endpoint config.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Collector base URL, parsed from <see cref="ConnectionString" />.</summary>
    public string BaseUrl =>
        SenderConnectionString.TryParse(ConnectionString, out var baseUrl, out _) ? baseUrl : string.Empty;

    /// <summary>Ingest key (sent as <c>X-Api-Key</c>), parsed from <see cref="ConnectionString" />.</summary>
    public string ApiKey =>
        SenderConnectionString.TryParse(ConnectionString, out _, out var apiKey) ? apiKey : string.Empty;
}