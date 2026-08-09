using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IInstallCoordinator
{
    InstallWorkflowState Current { get; }
    Task<InstallWorkflowResult> RunAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);
    Task<InstallWorkflowResult> RepairAsync(
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);
}
