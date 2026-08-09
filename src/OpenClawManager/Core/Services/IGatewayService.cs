using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IGatewayService
{
    Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<CommandResult> InstallAsync(CancellationToken cancellationToken);
    Task<CommandResult> StartAsync(CancellationToken cancellationToken);
    Task<CommandResult> StopAsync(CancellationToken cancellationToken);
    Task<CommandResult> RestartAsync(CancellationToken cancellationToken);
    Task<CommandResult> UninstallAsync(CancellationToken cancellationToken);
}
