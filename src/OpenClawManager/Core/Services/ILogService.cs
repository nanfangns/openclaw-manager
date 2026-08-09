using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface ILogService
{
    event EventHandler<LogEntry>? EntryAdded;
    void Write(AppLogLevel level, string message, IReadOnlyDictionary<string, string>? fields = null);
    string Redact(string text);
}
