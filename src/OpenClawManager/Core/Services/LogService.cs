using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class LogService : ILogService
{
    private static readonly Regex SecretPattern = new(
        @"(?i)([""']?(?:api[_-]?key|token|secret|password|authorization)[""']?\s*[:=]\s*[""']?)([^""'\s,;}\]]+)",
        RegexOptions.Compiled);

    private readonly PathLayout _paths;
    private readonly object _sync = new();

    public LogService(PathLayout paths)
    {
        _paths = paths;
        _paths.EnsureDataDirectories();
    }

    public event EventHandler<LogEntry>? EntryAdded;

    public void Write(AppLogLevel level, string message, IReadOnlyDictionary<string, string>? fields = null)
    {
        var safeMessage = Redact(message);
        var safeFields = fields?.ToDictionary(x => x.Key, x => Redact(x.Value));
        var entry = new LogEntry(DateTimeOffset.Now, level, safeMessage, safeFields);

        lock (_sync)
        {
            _paths.EnsureDataDirectories();
            var file = Path.Combine(_paths.LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            File.AppendAllText(file, line);
        }

        EntryAdded?.Invoke(this, entry);
    }

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return SecretPattern.Replace(text, "$1[REDACTED]");
    }
}
