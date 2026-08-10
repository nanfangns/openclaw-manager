using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IInstallationVerifier
{
    Task<VerificationReport> VerifyAsync(
        ModelConfiguration? model,
        bool requireGateway,
        bool probeModel,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken);
}
