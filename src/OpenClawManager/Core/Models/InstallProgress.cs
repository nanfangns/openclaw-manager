namespace OpenClawManager.Core.Models;

public sealed record InstallProgress(
    InstallStep Step,
    int Percent,
    string Message,
    bool IsError = false);
