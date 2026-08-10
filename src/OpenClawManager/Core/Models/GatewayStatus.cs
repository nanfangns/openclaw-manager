namespace OpenClawManager.Core.Models;

public sealed record GatewayStatus(
    bool IsInstalled,
    bool IsRunning,
    bool IsHealthy,
    int Port,
    string Summary,
    string? Detail = null,
    string Host = "127.0.0.1",
    bool? ConnectivityProbeSucceeded = null,
    int? ProcessId = null);
