namespace OpenClawManager.Core.Models;

public sealed record ProcessRunOptions(
    TimeSpan Timeout,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null)
{
    public static ProcessRunOptions Default { get; } = new(TimeSpan.FromMinutes(5));
}
