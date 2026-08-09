namespace OpenClawManager.Core.Models;

public sealed record GatewayStatus(
    bool IsInstalled,
    bool IsRunning,
    bool IsHealthy,
    int Port,
    string Summary,
    string? Detail = null);
