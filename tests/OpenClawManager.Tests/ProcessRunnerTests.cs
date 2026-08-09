using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;

namespace OpenClawManager.Tests;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task Returns_success_and_output_for_dotnet_version()
    {
        var result = await _runner.RunAsync(
            Environment.ProcessPath!,
            new[] { "--version" },
            new ProcessRunOptions(TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.StandardOutput);
    }

    [Fact]
    public async Task Returns_non_zero_for_invalid_dotnet_argument()
    {
        var result = await _runner.RunAsync(
            Environment.ProcessPath!,
            new[] { "--not-a-real-dotnet-option" },
            new ProcessRunOptions(TimeSpan.FromSeconds(10)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Returns_timeout_for_sleeping_process()
    {
        var result = await _runner.RunAsync(
            "powershell.exe",
            new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 3" },
            new ProcessRunOptions(TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.False(result.IsSuccess);
    }
}
