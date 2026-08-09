using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface IConfigService
{
    Task<string> BackupAsync(CancellationToken cancellationToken);
    Task<CommandResult> ConfigureModelAsync(ModelConfiguration configuration, CancellationToken cancellationToken);
    Task<CommandResult> ValidateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken);
    Task RestoreAsync(string backupPath, CancellationToken cancellationToken);
}
