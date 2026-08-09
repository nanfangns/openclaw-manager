namespace OpenClawManager.Core.Models;

public sealed record EnvironmentSnapshot(
    string WindowsVersion,
    string Architecture,
    bool IsSupportedWindows,
    bool Is64Bit,
    bool HasNetwork,
    double FreeDiskGb,
    string? NodePath,
    string? NodeVersion,
    string? NpmPath,
    string? NpmVersion,
    string? OpenClawPath,
    string? OpenClawVersion,
    bool GatewayPortInUse,
    int GatewayPort,
    string? GatewayPortOwner)
{
    public bool HasCompatibleNode => Version.TryParse(NodeVersion?.TrimStart('v'), out var version)
        && version.Major >= 22;
}
