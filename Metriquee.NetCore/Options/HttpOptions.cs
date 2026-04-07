namespace Metriquee.NetCore.Options;

public sealed record HttpOptions
{
    // Indicates whether HTTP logging is enabled
    public bool IsEnabled { get; set; } = true;

    // Indicates whether to capture HTTP request bodies
    public bool ShouldCaptureRequestBody { get; set; }

    // Indicates whether to capture HTTP response bodies
    public bool ShouldCaptureResponseBody { get; set; }

    // Maximum size of the HTTP body to log (in bytes)
    public int MaxBodyBytes { get; set; } = 4096;

    // URL paths to exclude from logging (case-insensitive prefix match). Example: "/api/health"
    public HashSet<string> ExcludedPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Header names whose values will be replaced with "***" in logs. Example: "Authorization"
    public HashSet<string> SensitiveHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie"
    };

    // JSON field names whose values will be replaced with "***" in logged bodies (case-insensitive). Example: "password"
    public HashSet<string> MaskedFields { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "password"
    };
}