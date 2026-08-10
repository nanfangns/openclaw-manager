using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public async Task Backup_and_restore_preserve_openclaw_files()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);
        Directory.CreateDirectory(paths.OpenClawHome);
        var configPath = Path.Combine(paths.OpenClawHome, "openclaw.json");
        await File.WriteAllTextAsync(configPath, "{\"before\":true}");

        var service = CreateService(paths);
        var backupPath = await service.BackupAsync(CancellationToken.None);

        await File.WriteAllTextAsync(configPath, "{\"before\":false}");
        await service.RestoreAsync(backupPath, CancellationToken.None);

        Assert.Equal("{\"before\":true}", await File.ReadAllTextAsync(configPath));
        Assert.True(File.Exists(Path.Combine(backupPath, "manifest.json")));
    }

    [Fact]
    public void Credential_service_redacts_secret_values()
    {
        var service = new CredentialService();

        var text = service.Redact("OPENAI_API_KEY=secret-value token:abc123");

        Assert.DoesNotContain("secret-value", text);
        Assert.DoesNotContain("abc123", text);
        Assert.Contains("[REDACTED]", text);
    }

    [Fact]
    public void Credential_service_redacts_json_secret_values()
    {
        var service = new CredentialService();

        var text = service.Redact("{\"token\":\"json-secret\",\"apiKey\":\"another-secret\"}");

        Assert.DoesNotContain("json-secret", text);
        Assert.DoesNotContain("another-secret", text);
        Assert.Contains("[REDACTED]", text);
    }

    private static ConfigService CreateService(PathLayout paths)
        => new(
            paths,
            new FakeRunner(),
            new CredentialService(),
            new LogService(paths));

    private sealed class FakeRunner : IProcessRunner
    {
        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            ProcessRunOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", string.Empty, TimeSpan.Zero, false, false));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "oc-config-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
