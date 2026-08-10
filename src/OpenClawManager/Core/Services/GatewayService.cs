using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class GatewayService : IGatewayService
{
    private readonly IProcessRunner _runner;
    private readonly ICredentialService _credentials;

    public GatewayService(IProcessRunner runner, ICredentialService? credentials = null)
    {
        _runner = runner;
        _credentials = credentials ?? new CredentialService();
    }

    public async Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawGatewayStatusJson(),
            new ProcessRunOptions(TimeSpan.FromSeconds(30)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var detail = string.Join(
                Environment.NewLine,
                new[] { result.StandardOutput, result.StandardError }
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
                .Trim();
            var missing = detail.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("command not found", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("找不到", StringComparison.OrdinalIgnoreCase);
            return new GatewayStatus(
                !missing,
                false,
                false,
                18789,
                missing ? "Gateway 未安装或 CLI 不可用" : "Gateway 状态检查失败",
                _credentials.Redact(detail));
        }

        if (GatewayStatusParser.TryParse(result.StandardOutput, out var status))
        {
            return status with { Detail = _credentials.Redact(status.Detail ?? string.Empty) };
        }

        var output = result.StandardOutput;
        var running = output.Contains("running", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("stopped", StringComparison.OrdinalIgnoreCase);
        var probeFailed = output.Contains("probe", StringComparison.OrdinalIgnoreCase)
            && (output.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || output.Contains("error", StringComparison.OrdinalIgnoreCase)
                || output.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        var healthy = running && !probeFailed;
        return new GatewayStatus(
            true,
            running,
            healthy,
            18789,
            healthy ? "Gateway 运行正常" : running ? "Gateway 正在运行，但连接探测未通过" : "Gateway 已安装但未运行",
            _credentials.Redact(output.Trim()),
            ConnectivityProbeSucceeded: probeFailed ? false : null);
    }

    public Task<CommandResult> InstallAsync(CancellationToken cancellationToken)
        => RunGatewayAsync(CommandCatalog.OpenClawGatewayInstall(), cancellationToken);

    public Task<CommandResult> StartAsync(CancellationToken cancellationToken)
        => RunGatewayAsync(CommandCatalog.OpenClawGatewayStart(), cancellationToken);

    public Task<CommandResult> StopAsync(CancellationToken cancellationToken)
        => RunGatewayAsync(CommandCatalog.OpenClawGatewayStop(), cancellationToken);

    public Task<CommandResult> RestartAsync(CancellationToken cancellationToken)
        => RunGatewayAsync(CommandCatalog.OpenClawGatewayRestart(), cancellationToken);

    public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken)
        => RunGatewayAsync(CommandCatalog.OpenClawGatewayUninstall(), cancellationToken);

    private Task<CommandResult> RunGatewayAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.OpenClaw,
            arguments,
            new ProcessRunOptions(TimeSpan.FromMinutes(2)),
            cancellationToken);

}
