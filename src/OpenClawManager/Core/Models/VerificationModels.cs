namespace OpenClawManager.Core.Models;

public enum VerificationCheckStatus
{
    Passed,
    Warning,
    Failed,
    Skipped
}

public sealed record VerificationCheck(
    string Id,
    string Name,
    VerificationCheckStatus Status,
    string Summary,
    string? Detail = null)
{
    public string StatusText => Status switch
    {
        VerificationCheckStatus.Passed => "通过",
        VerificationCheckStatus.Warning => "注意",
        VerificationCheckStatus.Failed => "失败",
        _ => "跳过"
    };
}

public sealed record VerificationReport(
    DateTimeOffset CheckedAt,
    bool Succeeded,
    IReadOnlyList<VerificationCheck> Checks,
    EnvironmentSnapshot? Environment = null,
    GatewayStatus? Gateway = null,
    ModelProbeResult? Model = null)
{
    public string Summary
    {
        get
        {
            var failed = Checks.Count(item => item.Status == VerificationCheckStatus.Failed);
            var warnings = Checks.Count(item => item.Status == VerificationCheckStatus.Warning);
            return failed > 0
                ? $"有 {failed} 项检查失败"
                : warnings > 0
                    ? $"检查完成，有 {warnings} 项需要注意"
                    : "所有检查均已通过";
        }
    }
}

public sealed record DiagnosticsReport(
    string ApplicationVersion,
    DateTimeOffset GeneratedAt,
    string ConfigPath,
    IReadOnlyList<string> ProcessPathEntries,
    VerificationReport Verification,
    IReadOnlyList<string> RecentLogs,
    InstallState State);
