using System.IO.Compression;
using System.Text.Json;
using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task Collect_redacts_sensitive_command_output()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);
        var logs = new LogService(paths);
        var service = new DiagnosticsService(
            paths,
            new FakeEnvironment(),
            new FakeVerifier(),
            new FakeStateStore(),
            logs);

        var report = await service.CollectAsync(CancellationToken.None);
        var json = JsonSerializer.Serialize(report);

        Assert.DoesNotContain("json-secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_contains_redacted_diagnostics_without_the_api_key()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);
        var logs = new LogService(paths);
        var service = new DiagnosticsService(
            paths,
            new FakeEnvironment(),
            new FakeVerifier(),
            new FakeStateStore(),
            logs);
        var report = new DiagnosticsReport(
            "1.0.0",
            DateTimeOffset.UtcNow,
            "C:\\Users\\tester\\.openclaw\\openclaw.json",
            Array.Empty<string>(),
            new VerificationReport(
                DateTimeOffset.UtcNow,
                true,
                new[] { new VerificationCheck("model", "模型", VerificationCheckStatus.Passed, "探测成功") }),
            Array.Empty<string>(),
            InstallState.Empty);

        var exportPath = await service.ExportAsync(report, CancellationToken.None);

        Assert.True(File.Exists(exportPath));
        using var archive = ZipFile.OpenRead(exportPath);
        var json = await new StreamReader(archive.GetEntry("diagnostics.json")!.Open()).ReadToEndAsync();
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("diagnostics.json", archive.Entries.Select(entry => entry.FullName));
    }

    private sealed class FakeEnvironment : IEnvironmentService
    {
        public Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken)
            => Task.FromResult(new EnvironmentSnapshot("Windows 11", "X64", true, true, true, 10, null, null, null, null, null, null, false, 18789, null));
    }

    private sealed class FakeVerifier : IInstallationVerifier
    {
        public Task<VerificationReport> VerifyAsync(ModelConfiguration? model, bool requireGateway, bool probeModel, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
            => Task.FromResult(new VerificationReport(
                DateTimeOffset.UtcNow,
                true,
                new[] { new VerificationCheck("gateway", "Gateway", VerificationCheckStatus.Failed, "探测失败", "{\"token\":\"json-secret\"}") }));
    }

    private sealed class FakeStateStore : IStateStore
    {
        public Task<InstallState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(InstallState.Empty);
        public Task SaveAsync(InstallState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
