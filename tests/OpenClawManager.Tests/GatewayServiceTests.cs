using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class GatewayServiceTests
{
    [Fact]
    public async Task Parses_nested_status_and_connectivity_probe()
    {
        var runner = new QueueRunner(new CommandResult(
            0,
            "{\"service\":{\"runtime\":{\"status\":\"running\",\"pid\":4321}},\"connectivity\":{\"status\":\"ok\"},\"host\":\"127.0.0.1\",\"port\":18789}",
            string.Empty,
            TimeSpan.Zero,
            false,
            false));
        var service = new GatewayService(runner);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsInstalled);
        Assert.True(status.IsRunning);
        Assert.True(status.IsHealthy);
        Assert.True(status.ConnectivityProbeSucceeded);
        Assert.Equal(18789, status.Port);
        Assert.Equal(4321, status.ProcessId);
        Assert.Equal("127.0.0.1", status.Host);
    }

    [Fact]
    public async Task Parses_json_with_cli_prefix_and_reports_failed_probe()
    {
        var runner = new QueueRunner(new CommandResult(
            0,
            "warning: using local config\n{\"runtime\":{\"status\":\"running\"},\"connectivity\":{\"status\":\"failed\"}}",
            string.Empty,
            TimeSpan.Zero,
            false,
            false));
        var service = new GatewayService(runner);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsRunning);
        Assert.False(status.IsHealthy);
        Assert.False(status.ConnectivityProbeSucceeded);
    }

    [Fact]
    public async Task Redacts_secret_values_from_status_detail()
    {
        var runner = new QueueRunner(new CommandResult(
            0,
            "{\"runtime\":{\"status\":\"running\"},\"token\":\"gateway-secret\"}",
            string.Empty,
            TimeSpan.Zero,
            false,
            false));
        var service = new GatewayService(runner);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.DoesNotContain("gateway-secret", status.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class QueueRunner(params CommandResult[] results) : IProcessRunner
    {
        private readonly Queue<CommandResult> _results = new(results);

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            ProcessRunOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(_results.Dequeue());
    }
}
