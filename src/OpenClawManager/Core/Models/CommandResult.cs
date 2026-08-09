namespace OpenClawManager.Core.Models;

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    bool Cancelled)
{
    public bool IsSuccess => ExitCode == 0 && !TimedOut && !Cancelled;
}
