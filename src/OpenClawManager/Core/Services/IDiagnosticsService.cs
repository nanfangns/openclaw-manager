using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IDiagnosticsService
{
    Task<DiagnosticsReport> CollectAsync(CancellationToken cancellationToken);
    Task<string> ExportAsync(DiagnosticsReport report, CancellationToken cancellationToken);
}
