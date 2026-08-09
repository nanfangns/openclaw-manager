namespace OpenClawManager.Core.Models;

public sealed record NodeInstallOptions(
    bool AllowInstall = true,
    string? PreferredVersion = null);

public sealed record NodeResult(
    bool Succeeded,
    string? Version,
    string? Path,
    bool InstalledByManager,
    string Summary);
