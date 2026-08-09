using System.Text.Json;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class GatewayService : IGatewayService
{
    private readonly IProcessRunner _runner;

    public GatewayService(IProcessRunner runner)
    {
        _runner = runner;
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
            return new GatewayStatus(false, false, false, 18789, "Gateway 未安装或状态不可用", result.StandardError);
        }

        if (TryParseJson(result.StandardOutput, out var status))
        {
            return status;
        }

        var output = result.StandardOutput;
        var running = output.Contains("running", StringComparison.OrdinalIgnoreCase)
            && !output.Contains("stopped", StringComparison.OrdinalIgnoreCase);
        var healthy = running || output.Contains("healthy", StringComparison.OrdinalIgnoreCase);
        return new GatewayStatus(true, running, healthy, 18789, running ? "Gateway 正在运行" : "Gateway 已安装但未运行", output.Trim());
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

    private static bool TryParseJson(string output, out GatewayStatus status)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                status = default!;
                return false;
            }

            var running = ReadBool(root, "running") || ReadString(root, "status")?.Equals("running", StringComparison.OrdinalIgnoreCase) == true;
            var healthy = ReadBool(root, "healthy") || running;
            var port = root.TryGetProperty("port", out var portElement) && portElement.TryGetInt32(out var parsedPort)
                ? parsedPort
                : 18789;
            status = new GatewayStatus(true, running, healthy, port, running ? "Gateway 正在运行" : "Gateway 已安装但未运行", output.Trim());
            return true;
        }
        catch (JsonException)
        {
            status = default!;
            return false;
        }
        catch (InvalidOperationException)
        {
            status = default!;
            return false;
        }
    }

    private static bool ReadBool(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
