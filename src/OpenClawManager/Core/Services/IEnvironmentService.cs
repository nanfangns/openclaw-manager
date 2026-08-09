using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IEnvironmentService
{
    Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken);
}
