using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IUninstallService
{
    Task<UninstallPreview> PreviewAsync(CancellationToken cancellationToken);
    Task<UninstallResult> ExecuteAsync(
        UninstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);
}
