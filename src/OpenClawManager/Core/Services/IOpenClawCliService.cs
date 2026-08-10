using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IOpenClawCliService
{
    Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken);
    Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken);
    Task<CommandResult> UninstallAsync(CancellationToken cancellationToken);
    Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken);
    Task<ModelProbeResult> ProbeModelAsync(
        ModelConfiguration? configuration,
        CancellationToken cancellationToken);
}
