namespace OpenClawManager.Core.Models;

public sealed record InstallState(
    string SchemaVersion,
    string? NodeVersion,
    string? OpenClawVersion,
    bool NodeInstalledByManager,
    bool OpenClawInstalledByManager,
    bool GatewayInstalledByManager,
    IReadOnlyList<string> OwnedShortcuts,
    IReadOnlyList<string> OwnedFirewallRules,
    IReadOnlyList<string> ConfigBackups,
    InstallStep CurrentStep)
{
    public static InstallState Empty { get; } = new(
        "1",
        null,
        null,
        false,
        false,
        false,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        InstallStep.NotStarted);
}
