using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IStateStore
{
    Task<InstallState> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(InstallState state, CancellationToken cancellationToken);
}
