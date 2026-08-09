using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class OpenClawCliService : IOpenClawCliService
{
    private readonly IProcessRunner _runner;
    private readonly ILogService _logs;

    public OpenClawCliService(IProcessRunner runner, ILogService logs)
    {
        _runner = runner;
        _logs = logs;
    }

    public async Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken)
    {
        var pathResult = await _runner.RunAsync(
            "where.exe",
            new[] { CommandCatalog.OpenClaw },
            new ProcessRunOptions(TimeSpan.FromSeconds(10)),
            cancellationToken);
        var path = pathResult.IsSuccess
            ? pathResult.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;

        var result = await _runner.RunAsync(
            path ?? CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawVersion(),
            new ProcessRunOptions(TimeSpan.FromSeconds(20)),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return new OpenClawVersionResult(false, null, path, "OpenClaw CLI 不可用");
        }

        return new OpenClawVersionResult(true, result.StandardOutput.Trim(), path, "OpenClaw 已安装");
    }

    public async Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken)
    {
        _logs.Write(AppLogLevel.Information, $"正在安装 OpenClaw {packageSpec}");
        var result = await _runner.RunAsync(
            CommandCatalog.Npm,
            CommandCatalog.NpmInstallGlobal(packageSpec),
            new ProcessRunOptions(TimeSpan.FromMinutes(10)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logs.Write(AppLogLevel.Error, "OpenClaw npm 安装失败", new Dictionary<string, string>
            {
                ["exitCode"] = result.ExitCode.ToString(),
                ["stderr"] = result.StandardError
            });
        }

        return result;
    }

    public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.Npm,
            CommandCatalog.NpmUninstallGlobal("openclaw"),
            new ProcessRunOptions(TimeSpan.FromMinutes(5)),
            cancellationToken);

    public Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawConfigValidate(),
            new ProcessRunOptions(TimeSpan.FromSeconds(60)),
            cancellationToken);

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawModelsList(),
            new ProcessRunOptions(TimeSpan.FromSeconds(60)),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return Array.Empty<string>();
        }

        return result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }
}
