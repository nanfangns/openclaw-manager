using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface INodeService
{
    Task<NodeResult> EnsureCompatibleAsync(
        NodeInstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);
}
