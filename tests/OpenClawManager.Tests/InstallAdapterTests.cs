using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class InstallAdapterTests
{
    [Fact]
    public async Task OpenClaw_install_propagates_npm_failure()
    {
        var runner = new FakeRunner(new CommandResult(1, string.Empty, "network failed", TimeSpan.Zero, false, false));
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var service = new OpenClawCliService(runner, logs);

        var result = await service.InstallAsync("openclaw@latest", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Node_service_does_not_download_when_install_is_disallowed()
    {
        var environment = new FakeEnvironment(new EnvironmentSnapshot(
            "Windows 10", "X64", true, true, true, 10, null, null, null, null, null, null, false, 18789, null));
        var service = new NodeService(
            environment,
            new FakeRunner(),
            new LogService(new OpenClawManager.Infrastructure.PathLayout(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))),
            new OpenClawManager.Infrastructure.AdminElevation());

        var result = await service.EnsureCompatibleAsync(
            new NodeInstallOptions(false),
            new Progress<InstallProgress>(),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("兼容", result.Summary);
    }

    private sealed class FakeEnvironment : IEnvironmentService
    {
        private readonly EnvironmentSnapshot _snapshot;
        public FakeEnvironment(EnvironmentSnapshot snapshot) => _snapshot = snapshot;
        public Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshot);
    }

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly CommandResult _result;
        public FakeRunner(CommandResult? result = null)
            => _result = result ?? new CommandResult(0, "ok", string.Empty, TimeSpan.Zero, false, false);

        public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, ProcessRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }
}
