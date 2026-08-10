using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class EnvironmentService : IEnvironmentService
{
    private const int GatewayPort = 18789;
    private readonly IProcessRunner _runner;
    private readonly HttpClient _httpClient;

    public EnvironmentService(IProcessRunner runner, HttpClient? httpClient = null)
    {
        _runner = runner;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public async Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken)
    {
        PathEnvironment.RefreshProcessPath();
        var nodePath = await FindExecutableAsync("node", cancellationToken);
        var npmPath = await FindExecutableAsync("npm", cancellationToken);
        var openClawPath = await FindExecutableAsync("openclaw", cancellationToken);

        var nodeVersion = await ReadVersionAsync(nodePath ?? "node", cancellationToken);
        var npmVersion = await ReadVersionAsync(npmPath ?? "npm", cancellationToken);
        var openClawVersion = await ReadVersionAsync(openClawPath ?? "openclaw", cancellationToken);

        var freeDiskGb = 0d;
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            try
            {
                freeDiskGb = new DriveInfo(systemRoot).AvailableFreeSpace / 1024d / 1024d / 1024d;
            }
            catch (IOException)
            {
                freeDiskGb = 0d;
            }
        }

        var hasNetwork = NetworkInterface.GetIsNetworkAvailable();
        if (hasNetwork)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    "https://www.microsoft.com",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                hasNetwork = response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                hasNetwork = false;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                hasNetwork = false;
            }
        }

        var gatewayPortInUse = IPGlobalProperties
            .GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == GatewayPort);

        return new EnvironmentSnapshot(
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture.ToString(),
            OperatingSystem.IsWindowsVersionAtLeast(10),
            Environment.Is64BitOperatingSystem,
            hasNetwork,
            Math.Round(freeDiskGb, 1),
            nodePath,
            nodeVersion,
            npmPath,
            npmVersion,
            openClawPath,
            openClawVersion,
            gatewayPortInUse,
            GatewayPort,
            null);
    }

    private async Task<string?> FindExecutableAsync(string name, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            "where.exe",
            new[] { name },
            new ProcessRunOptions(TimeSpan.FromSeconds(10)),
            cancellationToken);
        return result.IsSuccess
            ? result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
            : null;
    }

    private async Task<string?> ReadVersionAsync(string executable, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            executable,
            new[] { "--version" },
            new ProcessRunOptions(TimeSpan.FromSeconds(20)),
            cancellationToken);
        return result.IsSuccess
            ? result.StandardOutput.Trim().Split(Environment.NewLine).FirstOrDefault()
            : null;
    }
}
