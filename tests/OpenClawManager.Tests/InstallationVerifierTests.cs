using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class InstallationVerifierTests
{
    [Fact]
    public async Task Fails_when_gateway_is_running_but_connectivity_probe_fails()
    {
        var environment = new FakeEnvironment();
        var openClaw = new FakeOpenClaw();
        var config = new FakeConfig();
        var gateway = new FakeGateway
        {
            Status = new GatewayStatus(true, true, false, 18789, "Gateway 连接探测失败", "rpc failed", ConnectivityProbeSucceeded: false)
        };
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var verifier = new InstallationVerifier(environment, openClaw, config, gateway, logs);

        var report = await verifier.VerifyAsync(null, true, false, null, CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Contains(report.Checks, item => item.Id == "gateway" && item.Status == VerificationCheckStatus.Failed);
    }

    [Fact]
    public async Task Treats_missing_model_as_warning_when_model_probe_is_requested()
    {
        var environment = new FakeEnvironment();
        var openClaw = new FakeOpenClaw
        {
            Probe = new ModelProbeResult(true, false, null, "未发现已配置模型")
        };
        var config = new FakeConfig();
        var gateway = new FakeGateway();
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var verifier = new InstallationVerifier(environment, openClaw, config, gateway, logs);

        var report = await verifier.VerifyAsync(null, false, true, null, CancellationToken.None);

        Assert.True(report.Succeeded);
        Assert.Contains(report.Checks, item => item.Id == "model" && item.Status == VerificationCheckStatus.Warning);
    }

    private sealed class FakeEnvironment : IEnvironmentService
    {
        public Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken)
            => Task.FromResult(new EnvironmentSnapshot(
                "Windows 11", "X64", true, true, true, 20,
                "node.exe", "v24.15.0", "npm.cmd", "11.0.0", "openclaw.cmd", "2026.8.1",
                false, 18789, null));
    }

    private sealed class FakeOpenClaw : IOpenClawCliService
    {
        public ModelProbeResult Probe { get; init; } = new(true, true, "openai/gpt-4o", "模型探测成功");
        public Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new OpenClawVersionResult(true, "2026.8.1", "openclaw.cmd", "ok"));
        public Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken)
            => Task.FromResult(Success());
        public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken)
            => Task.FromResult(Success());
        public Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken)
            => Task.FromResult(Success());
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<ModelProbeResult> ProbeModelAsync(ModelConfiguration? configuration, CancellationToken cancellationToken)
            => Task.FromResult(Probe);
    }

    private sealed class FakeConfig : IConfigService
    {
        public Task<string> BackupAsync(CancellationToken cancellationToken) => Task.FromResult("backup");
        public Task<CommandResult> ConfigureModelAsync(ModelConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConfigBackup>>(Array.Empty<ConfigBackup>());
        public Task RestoreAsync(string backupPath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGateway : IGatewayService
    {
        public GatewayStatus Status { get; init; } = new(true, true, true, 18789, "Gateway 正常", ConnectivityProbeSucceeded: true);
        public Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(Status);
        public Task<CommandResult> InstallAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> StartAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> StopAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> RestartAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
    }

    private static CommandResult Success() => new(0, string.Empty, string.Empty, TimeSpan.Zero, false, false);
}
