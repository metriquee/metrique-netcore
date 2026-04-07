namespace Metriquee.NetCore.Options;

public sealed record ExceptionOptions
{
    // Indicates whether exception logging is enabled.
    public bool IsEnabled { get; set; } = true;

    // Indicates whether to include stack traces in the logged exceptions.
    public bool IncludeStackTrace { get; set; } = true;

    // Maximum number of stack trace lines to include.
    public int MaxStackTraceLines { get; set; } = 100;

    // Exception type full names to exclude from logging. Example: "System.Threading.ThreadAbortException"
    public HashSet<string> ExcludedExceptions { get; set; } = new(StringComparer.Ordinal);
}