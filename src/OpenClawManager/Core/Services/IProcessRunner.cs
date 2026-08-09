using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IProcessRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ProcessRunOptions options,
        CancellationToken cancellationToken);
}
