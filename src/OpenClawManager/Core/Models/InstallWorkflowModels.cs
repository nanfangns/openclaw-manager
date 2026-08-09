namespace OpenClawManager.Core.Models;

public sealed record InstallOptions(
    ModelConfiguration? Model = null,
    bool InstallNodeIfMissing = true,
    bool InstallGateway = true);

public sealed record InstallWorkflowState(
    InstallStep Step,
    int Percent,
    string Message,
    bool IsRunning,
    bool IsCompleted,
    bool IsFailed);

public sealed record InstallWorkflowResult(
    bool Succeeded,
    InstallStep FailedStep,
    string Summary);
