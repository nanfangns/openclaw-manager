using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class ConfigService : IConfigService
{
    private readonly PathLayout _paths;
    private readonly IProcessRunner _runner;
    private readonly ICredentialService _credentials;
    private readonly ILogService _logs;

    public ConfigService(
        PathLayout paths,
        IProcessRunner runner,
        ICredentialService credentials,
        ILogService logs)
    {
        _paths = paths;
        _runner = runner;
        _credentials = credentials;
        _logs = logs;
    }

    public async Task<string> BackupAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureDataDirectories();
        var backupPath = Path.Combine(
            _paths.BackupsDirectory,
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(backupPath);

        var fileCount = 0;
        if (Directory.Exists(_paths.OpenClawHome))
        {
            foreach (var source in Directory.EnumerateFiles(_paths.OpenClawHome, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(_paths.OpenClawHome, source);
                var destination = Path.Combine(backupPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = File.OpenRead(source);
                await using var output = File.Create(destination);
                await input.CopyToAsync(output, cancellationToken);
                fileCount++;
            }
        }

        var manifest = new
        {
            Source = _paths.OpenClawHome,
            CreatedAt = DateTimeOffset.Now,
            FileCount = fileCount,
            Files = Directory.Exists(backupPath)
                ? Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories)
                    .Select(path => new
                    {
                        Path = Path.GetRelativePath(backupPath, path),
                        Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
                    })
                    .ToArray()
                : Array.Empty<object>()
        };
        var manifestPath = Path.Combine(backupPath, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logs.Write(AppLogLevel.Information, "OpenClaw 配置已备份", new Dictionary<string, string>
        {
            ["path"] = backupPath,
            ["fileCount"] = fileCount.ToString()
        });
        return backupPath;
    }

    public async Task<CommandResult> ConfigureModelAsync(
        ModelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var provider = ModelProviderCatalog.Find(configuration.ProviderId);
        if (provider is null)
        {
            return FailedResult($"未知模型提供商: {configuration.ProviderId}");
        }

        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return FailedResult("API Key 不能为空。");
        }

        var args = new List<string>
        {
            "onboard",
            "--non-interactive",
            "--accept-risk",
            "--mode",
            "local",
            "--auth-choice",
            provider.AuthChoice,
            "--secret-input-mode",
            "ref",
            "--skip-ui",
            "--skip-channels",
            "--skip-skills",
            "--skip-search",
            "--skip-daemon"
        };

        if (provider.RequiresBaseUrl && !string.IsNullOrWhiteSpace(configuration.BaseUrl))
        {
            args.Add("--custom-base-url");
            args.Add(configuration.BaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(configuration.ModelId))
        {
            args.Add("--custom-model-id");
            args.Add(configuration.ModelId);
        }

        var environment = new Dictionary<string, string?>
        {
            [provider.EnvironmentVariable ?? "CUSTOM_API_KEY"] = configuration.ApiKey
        };
        var result = await _runner.RunAsync(
            CommandCatalog.OpenClaw,
            args,
            new ProcessRunOptions(TimeSpan.FromMinutes(3), Environment: environment),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logs.Write(AppLogLevel.Error, "模型配置失败", new Dictionary<string, string>
            {
                ["provider"] = provider.Id,
                ["exitCode"] = result.ExitCode.ToString(),
                ["stderr"] = _credentials.Redact(result.StandardError)
            });
        }

        return result;
    }

    public Task<CommandResult> ValidateAsync(CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawConfigValidate(),
            new ProcessRunOptions(TimeSpan.FromSeconds(60)),
            cancellationToken);

    public Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureDataDirectories();
        var backups = Directory.EnumerateDirectories(_paths.BackupsDirectory)
            .Select(path =>
            {
                var manifest = Path.Combine(path, "manifest.json");
                var fileCount = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Count(file => !string.Equals(file, manifest, StringComparison.OrdinalIgnoreCase));
                return new ConfigBackup(
                    path,
                    Directory.GetCreationTimeUtc(path),
                    fileCount,
                    manifest);
            })
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ConfigBackup>>(backups);
    }

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException(backupPath);
        }

        Directory.CreateDirectory(_paths.OpenClawHome);
        foreach (var source in Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(backupPath, source);
            var destination = Path.Combine(_paths.OpenClawHome, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = File.OpenRead(source);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, cancellationToken);
        }

        _logs.Write(AppLogLevel.Information, "OpenClaw 配置已恢复");
    }

    private static CommandResult FailedResult(string message)
        => new(-1, string.Empty, message, TimeSpan.Zero, false, false);
}
