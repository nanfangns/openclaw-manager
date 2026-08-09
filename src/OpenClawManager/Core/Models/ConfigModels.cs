namespace OpenClawManager.Core.Models;

public sealed record ModelConfiguration(
    string ProviderId,
    string? ModelId,
    string? ApiKey,
    string? BaseUrl = null);

public sealed record ConfigBackup(
    string Path,
    DateTimeOffset CreatedAt,
    int FileCount,
    string ManifestPath);

public sealed record SecretInput(string Value);
