namespace OpenClawManager.Core.Models;

public enum InstallStep
{
    NotStarted,
    Detecting,
    InstallingNode,
    VerifyingNode,
    InstallingOpenClaw,
    VerifyingOpenClaw,
    BackingUpConfig,
    ConfiguringModel,
    InstallingGateway,
    StartingGateway,
    HealthChecking,
    Completed,
    Failed,
    Cancelled
}
