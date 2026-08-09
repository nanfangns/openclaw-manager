namespace OpenClawManager.Core.Models;

public sealed record OpenClawVersionResult(
    bool Succeeded,
    string? Version,
    string? Path,
    string Summary);
