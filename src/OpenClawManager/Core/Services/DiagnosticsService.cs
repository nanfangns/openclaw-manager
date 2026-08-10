using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly PathLayout _paths;
    private readonly IEnvironmentService _environment;
    private readonly IInstallationVerifier _verifier;
    private readonly IStateStore _stateStore;
    private readonly ILogService _logs;

    public DiagnosticsService(
        PathLayout paths,
        IEnvironmentService environment,
        IInstallationVerifier verifier,
        IStateStore stateStore,
        ILogService logs)
    {
        _paths = paths;
        _environment = environment;
        _verifier = verifier;
        _stateStore = stateStore;
        _logs = logs;
    }

    public async Task<DiagnosticsReport> CollectAsync(CancellationToken cancellationToken)
    {
        var verification = await _verifier.VerifyAsync(null, true, true, null, cancellationToken);
        if (verification.Environment is null)
        {
            verification = verification with
            {
                Environment = await _environment.DetectAsync(cancellationToken)
            };
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        var sanitizedVerification = SanitizeVerification(verification);
        return new DiagnosticsReport(
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            DateTimeOffset.Now,
            SanitizePath(_paths.OpenClawConfigFile),
            PathEnvironment.GetProcessPathEntries().Select(SanitizePath).ToArray(),
            sanitizedVerification,
            ReadRecentLogs(),
            SanitizeState(state));
    }

    public async Task<string> ExportAsync(DiagnosticsReport report, CancellationToken cancellationToken)
    {
        _paths.EnsureDataDirectories();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exportPath = Path.Combine(
            _paths.DiagnosticsDirectory,
            $"OpenClawManager-diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{suffix}.zip");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        await using var stream = File.Create(exportPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var jsonEntry = archive.CreateEntry("diagnostics.json", CompressionLevel.Fastest);
        await using (var jsonStream = jsonEntry.Open())
        {
            await JsonSerializer.SerializeAsync(jsonStream, report, options, cancellationToken);
        }

        var logsEntry = archive.CreateEntry("recent-logs.txt", CompressionLevel.Fastest);
        await using (var logsStream = new StreamWriter(logsEntry.Open(), Encoding.UTF8))
        {
            await logsStream.WriteAsync(string.Join(Environment.NewLine, report.RecentLogs));
        }

        _logs.Write(AppLogLevel.Information, "诊断包已导出", new Dictionary<string, string>
        {
            ["path"] = exportPath
        });
        return exportPath;
    }

    private VerificationReport SanitizeVerification(VerificationReport report)
        => report with
        {
            Checks = report.Checks
                .Select(check => check with { Detail = _logs.Redact(check.Detail ?? string.Empty) })
                .ToArray(),
            Environment = report.Environment is null ? null : SanitizeEnvironment(report.Environment),
            Gateway = report.Gateway is null ? null : report.Gateway with
            {
                Detail = _logs.Redact(report.Gateway.Detail ?? string.Empty)
            },
            Model = report.Model is null ? null : report.Model with
            {
                Detail = _logs.Redact(report.Model.Detail ?? string.Empty)
            }
        };

    private EnvironmentSnapshot SanitizeEnvironment(EnvironmentSnapshot snapshot)
        => snapshot with
        {
            NodePath = SanitizePath(snapshot.NodePath),
            NpmPath = SanitizePath(snapshot.NpmPath),
            OpenClawPath = SanitizePath(snapshot.OpenClawPath)
        };

    private InstallState SanitizeState(InstallState state)
        => state with
        {
            OwnedShortcuts = state.OwnedShortcuts.Select(SanitizePath).ToArray(),
            ConfigBackups = state.ConfigBackups.Select(SanitizePath).ToArray()
        };

    private IReadOnlyList<string> ReadRecentLogs()
    {
        try
        {
            if (!Directory.Exists(_paths.LogsDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(_paths.LogsDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(3)
                .SelectMany(File.ReadLines)
                .TakeLast(120)
                .Select(line => _logs.Redact(line))
                .ToArray();
        }
        catch (IOException ex)
        {
            return new[] { $"读取日志失败：{ex.Message}" };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new[] { $"读取日志失败：{ex.Message}" };
        }
    }

    private string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace(_paths.UserProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }
}
