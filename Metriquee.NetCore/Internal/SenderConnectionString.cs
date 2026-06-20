namespace Metriquee.NetCore.Internal;

/// <summary>
///     Parses the combined sender connection string — <c>scheme://&lt;apiKey&gt;@host[:port][/path]</c> —
///     into its collector base URL and ingest key. Mirrors the value the dashboard mints on registration.
/// </summary>
internal static class SenderConnectionString
{
    public static bool TryParse(string? value, out string baseUrl, out string apiKey)
    {
        baseUrl = string.Empty;
        apiKey = string.Empty;

        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return false;
        if (string.IsNullOrEmpty(uri.UserInfo)) return false;

        apiKey = Uri.UnescapeDataString(uri.UserInfo);
        if (string.IsNullOrEmpty(apiKey)) return false;

        // Reconstruct the base URL without the userinfo (Authority excludes it).
        var path = uri.AbsolutePath.TrimEnd('/');
        baseUrl = $"{uri.Scheme}://{uri.Authority}{path}";
        return true;
    }
}