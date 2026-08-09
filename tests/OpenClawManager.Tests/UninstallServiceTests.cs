using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class UninstallServiceTests
{
    [Fact]
    public async Task Removes_only_manager_owned_cli_and_gateway_and_preserves_config_by_default()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = new PathLayout(root, root);
        Directory.CreateDirectory(paths.OpenClawHome);
        await File.WriteAllTextAsync(paths.OpenClawConfigFile, "{}", CancellationToken.None);
        var state = InstallState.Empty with
        {
            OpenClawInstalledByManager = true,
            GatewayInstalledByManager = true
        };
        var set = new FakeSet(paths, state);
        var service = set.CreateService();

        var result = await service.ExecuteAsync(
            new UninstallOptions(true, false, false, false, true),
            new Progress<InstallProgress>(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(paths.OpenClawConfigFile));
        Assert.True(set.OpenClaw.UninstallCalled);
        Assert.True(set.Gateway.StopCalled);
        Assert.True(set.Gateway.UninstallCalled);
        Assert.False(set.State.OpenClawInstalledByManager);
        Assert.False(set.State.GatewayInstalledByManager);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Backs_up_then_removes_config_when_explicitly_selected()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = new PathLayout(root, root);
        Directory.CreateDirectory(paths.OpenClawHome);
        await File.WriteAllTextAsync(paths.OpenClawConfigFile, "{\"keep\":true}", CancellationToken.None);
        var set = new FakeSet(paths, InstallState.Empty);

        var result = await set.CreateService().ExecuteAsync(
            new UninstallOptions(false, false, true, false, false),
            new Progress<InstallProgress>(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(Directory.Exists(paths.OpenClawHome));
        Assert.True(set.Config.BackupCalled);
        Assert.NotEmpty(Directory.EnumerateDirectories(paths.BackupsDirectory));
        Directory.Delete(root, true);
    }

    private sealed class FakeSet
    {
        public FakeSet(PathLayout paths, InstallState state)
        {
            Paths = paths;
            State = state;
            Logs = new LogService(paths);
            Config = new FakeConfig(paths);
            OpenClaw = new FakeOpenClaw();
            Gateway = new FakeGateway();
        }

        public PathLayout Paths { get; }
        public InstallState State { get; set; }
        public LogService Logs { get; }
        public FakeConfig Config { get; }
        public FakeOpenClaw OpenClaw { get; }
        public FakeGateway Gateway { get; }

        public UninstallService CreateService()
            => new(Paths, new FakeStateStore(this), OpenClaw, Gateway, Config, new AdminElevation(), Logs);
    }

    private sealed class FakeStateStore(FakeSet set) : IStateStore
    {
        public Task<InstallState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(set.State);
        public Task SaveAsync(InstallState state, CancellationToken cancellationToken) { set.State = state; return Task.CompletedTask; }
    }

    private sealed class FakeOpenClaw : IOpenClawCliService
    {
        public bool UninstallCalled { get; private set; }
        public Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(new OpenClawVersionResult(false, null, null, "missing"));
        public Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken) { UninstallCalled = true; return Task.FromResult(Success()); }
        public Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeGateway : IGatewayService
    {
        public bool StopCalled { get; private set; }
        public bool UninstallCalled { get; private set; }
        public Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new GatewayStatus(false, false, false, 18789, "missing"));
        public Task<CommandResult> InstallAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> StartAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> StopAsync(CancellationToken cancellationToken) { StopCalled = true; return Task.FromResult(Success()); }
        public Task<CommandResult> RestartAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken) { UninstallCalled = true; return Task.FromResult(Success()); }
    }

    private sealed class FakeConfig(PathLayout paths) : IConfigService
    {
        public bool BackupCalled { get; private set; }
        public Task<string> BackupAsync(CancellationToken cancellationToken)
        {
            BackupCalled = true;
            var backup = Path.Combine(paths.BackupsDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            return Task.FromResult(backup);
        }
        public Task<CommandResult> ConfigureModelAsync(ModelConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<CommandResult> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(Success());
        public Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConfigBackup>>(Array.Empty<ConfigBackup>());
        public Task RestoreAsync(string backupPath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CommandResult Success() => new(0, string.Empty, string.Empty, TimeSpan.Zero, false, false);
}
