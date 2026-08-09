using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class InstallCoordinatorTests
{
    [Fact]
    public async Task Runs_all_required_steps_and_persists_completed_state()
    {
        var fakes = FakeSet.Success();
        var coordinator = fakes.CreateCoordinator();
        var progress = new Progress<InstallProgress>();

        var result = await coordinator.RunAsync(
            new InstallOptions(new ModelConfiguration("openai", "gpt-4o", "secret")),
            progress,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InstallStep.Completed, result.FailedStep);
        Assert.Equal(InstallStep.Completed, coordinator.Current.Step);
        Assert.True(fakes.State.CurrentStep == InstallStep.Completed);
        Assert.True(fakes.Gateway.Started);
    }

    [Fact]
    public async Task Stops_and_reports_failure_when_gateway_start_fails()
    {
        var fakes = FakeSet.Success();
        fakes.Gateway.StartResult = new CommandResult(1, string.Empty, "failed", TimeSpan.Zero, false, false);
        var coordinator = fakes.CreateCoordinator();

        var result = await coordinator.RunAsync(new InstallOptions(), new Progress<InstallProgress>(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(InstallStep.StartingGateway, result.FailedStep);
    }

    private sealed class FakeSet
    {
        public FakeSet()
        {
            Environment = new FakeEnvironment();
            Node = new FakeNode();
            OpenClaw = new FakeOpenClaw();
            Config = new FakeConfig();
            Gateway = new FakeGateway();
            State = InstallState.Empty;
            Logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }

        public FakeEnvironment Environment { get; }
        public FakeNode Node { get; }
        public FakeOpenClaw OpenClaw { get; }
        public FakeConfig Config { get; }
        public FakeGateway Gateway { get; }
        public InstallState State { get; set; }
        public LogService Logs { get; }

        public static FakeSet Success() => new();

        public InstallCoordinator CreateCoordinator()
            => new(
                Environment,
                Node,
                OpenClaw,
                Config,
                Gateway,
                new FakeStateStore(this),
                Logs);
    }

    private sealed class FakeEnvironment : IEnvironmentService
    {
        public Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken)
            => Task.FromResult(new EnvironmentSnapshot(
                "Windows 10", "X64", true, true, true, 10,
                "node", "v24.15.0", "npm", "11", "openclaw", "2026.7.1",
                false, 18789, null));
    }

    private sealed class FakeNode : INodeService
    {
        public Task<NodeResult> EnsureCompatibleAsync(NodeInstallOptions options, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
            => Task.FromResult(new NodeResult(true, "v24.15.0", "node", false, "ok"));
    }

    private sealed class FakeOpenClaw : IOpenClawCliService
    {
        public Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new OpenClawVersionResult(true, "2026.7.1", "openclaw", "ok"));
        public Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        public Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeConfig : IConfigService
    {
        public Task<string> BackupAsync(CancellationToken cancellationToken) => Task.FromResult("backup");
        public Task<CommandResult> ConfigureModelAsync(ModelConfiguration configuration, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        public Task<CommandResult> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        public Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ConfigBackup>>(Array.Empty<ConfigBackup>());
        public Task RestoreAsync(string backupPath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGateway : IGatewayService
    {
        public bool Started { get; private set; }
        public CommandResult StartResult { get; set; } = new(0, string.Empty, string.Empty, TimeSpan.Zero, false, false);
        public Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(new GatewayStatus(true, Started, Started, 18789, Started ? "running" : "stopped"));
        public Task<CommandResult> InstallAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        public Task<CommandResult> StartAsync(CancellationToken cancellationToken)
        {
            if (StartResult.IsSuccess) Started = true;
            return Task.FromResult(StartResult);
        }
        public Task<CommandResult> StopAsync(CancellationToken cancellationToken)
        {
            Started = false;
            return Task.FromResult(new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero, false, false));
        }
        public Task<CommandResult> RestartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);
        public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);
    }

    private sealed class FakeStateStore : IStateStore
    {
        private readonly FakeSet _set;
        public FakeStateStore(FakeSet set) => _set = set;
        public Task<InstallState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_set.State);
        public Task SaveAsync(InstallState state, CancellationToken cancellationToken)
        {
            _set.State = state;
            return Task.CompletedTask;
        }
    }
}
