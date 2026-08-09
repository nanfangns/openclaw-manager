namespace OpenClawManager.Core.Models;

public sealed record UninstallOptions(
    bool RemoveOpenClaw,
    bool RemoveNode,
    bool RemoveConfig,
    bool RemoveWorkspace,
    bool RemoveGateway);

public sealed record UninstallPreview(
    bool GatewayInstalled,
    bool OpenClawInstalled,
    bool NodeInstalledByManager,
    bool HasConfig,
    IReadOnlyList<string> OwnedShortcuts,
    string Summary);

public sealed record UninstallResult(
    bool Succeeded,
    string Summary);
