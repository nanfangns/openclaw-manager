namespace OpenClawManager.Core.Models;

public enum AppLogLevel
{
    Trace,
    Information,
    Warning,
    Error
}

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    AppLogLevel Level,
    string Message,
    IReadOnlyDictionary<string, string>? Fields = null);
