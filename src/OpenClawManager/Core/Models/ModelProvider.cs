namespace OpenClawManager.Core.Models;

public sealed record ModelProvider(
    string Id,
    string DisplayName,
    string AuthChoice,
    string? EnvironmentVariable,
    bool RequiresBaseUrl = false);
