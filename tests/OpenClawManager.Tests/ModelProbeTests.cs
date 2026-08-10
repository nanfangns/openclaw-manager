using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class ModelProbeTests
{
    [Fact]
    public async Task Reports_success_for_a_live_model_probe()
    {
        var runner = new QueueRunner(new CommandResult(
            0,
            "{\"model\":\"openai/gpt-4o\",\"probe\":{\"status\":\"ok\"}}",
            string.Empty,
            TimeSpan.Zero,
            false,
            false));
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var service = new OpenClawCliService(runner, logs);

        var result = await service.ProbeModelAsync(
            new ModelConfiguration("openai", "gpt-4o", "secret"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.IsConfigured);
        Assert.Equal("openai/gpt-4o", result.Model);
    }

    [Fact]
    public async Task Reports_unknown_model_without_treating_provider_auth_as_success()
    {
        var runner = new QueueRunner(new CommandResult(
            1,
            "{\"model\":\"deepseek/deepseek-chat\",\"probe\":{\"status\":\"error\",\"message\":\"unknown model\"}}",
            "unknown model",
            TimeSpan.Zero,
            false,
            false));
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var service = new OpenClawCliService(runner, logs);

        var result = await service.ProbeModelAsync(null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("unknown model", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ignores_null_error_metadata_in_a_successful_probe()
    {
        var runner = new QueueRunner(new CommandResult(
            0,
            "{\"model\":\"openai/gpt-4o\",\"status\":\"ok\",\"error\":null}",
            string.Empty,
            TimeSpan.Zero,
            false,
            false));
        var logs = new LogService(new OpenClawManager.Infrastructure.PathLayout(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var service = new OpenClawCliService(runner, logs);

        var result = await service.ProbeModelAsync(null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.IsConfigured);
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
