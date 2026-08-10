namespace OpenClawManager.Core.Models;

public sealed record OpenClawVersionResult(
    bool Succeeded,
    string? Version,
    string? Path,
    string Summary);

public sealed record ModelProbeResult(
    bool Succeeded,
    bool IsConfigured,
    string? Model,
    string Summary,
    string? Detail = null);
