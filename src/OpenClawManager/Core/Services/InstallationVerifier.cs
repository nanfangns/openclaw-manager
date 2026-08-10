using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public sealed class InstallationVerifier : IInstallationVerifier
{
    private readonly IEnvironmentService _environment;
    private readonly IOpenClawCliService _openClaw;
    private readonly IConfigService _config;
    private readonly IGatewayService _gateway;
    private readonly ILogService _logs;

    public InstallationVerifier(
        IEnvironmentService environment,
        IOpenClawCliService openClaw,
        IConfigService config,
        IGatewayService gateway,
        ILogService logs)
    {
        _environment = environment;
        _openClaw = openClaw;
        _config = config;
        _gateway = gateway;
        _logs = logs;
    }

    public async Task<VerificationReport> VerifyAsync(
        ModelConfiguration? model,
        bool requireGateway,
        bool probeModel,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var checks = new List<VerificationCheck>();
        EnvironmentSnapshot? environment = null;
        GatewayStatus? gateway = null;
        ModelProbeResult? modelProbe = null;

        Report(progress, InstallStep.Detecting, 5, "正在重新读取运行环境");
        try
        {
            environment = await _environment.DetectAsync(cancellationToken);
            AddEnvironmentChecks(checks, environment);
        }
        catch (Exception ex)
        {
            checks.Add(Failed("environment", "运行环境", "环境检测失败", ex.Message));
        }

        Report(progress, InstallStep.VerifyingOpenClaw, 25, "正在验证 OpenClaw CLI");
        var version = await _openClaw.GetVersionAsync(cancellationToken);
        if (version.Succeeded && !string.IsNullOrWhiteSpace(version.Version))
        {
            checks.Add(Passed("openclaw", "OpenClaw CLI", $"已发现 {version.Version}", version.Path));
        }
        else
        {
            checks.Add(Failed("openclaw", "OpenClaw CLI", "未找到可用的 OpenClaw CLI", version.Summary));
        }

        if (version.Succeeded)
        {
            Report(progress, InstallStep.HealthChecking, 45, "正在校验 OpenClaw 配置");
            var config = await _config.ValidateAsync(cancellationToken);
            checks.Add(config.IsSuccess
                ? Passed("config", "OpenClaw 配置", "配置校验通过")
                : Failed("config", "OpenClaw 配置", "配置校验失败", Combine(config)));
        }
        else
        {
            checks.Add(Skipped("config", "OpenClaw 配置", "CLI 不可用，暂时无法校验"));
        }

        if (requireGateway)
        {
            Report(progress, InstallStep.HealthChecking, 65, "正在探测 Gateway 服务");
            gateway = await _gateway.GetStatusAsync(cancellationToken);
            checks.Add(gateway.IsHealthy
                ? Passed("gateway", "Gateway 连接", $"{gateway.Host}:{gateway.Port} 可用", gateway.Detail)
                : Failed("gateway", "Gateway 连接", gateway.Summary, gateway.Detail));
        }
        else
        {
            checks.Add(Skipped("gateway", "Gateway 连接", "安装选项未要求 Gateway"));
        }

        if (probeModel)
        {
            Report(progress, InstallStep.ConfiguringModel, 82, "正在探测模型服务");
            modelProbe = await _openClaw.ProbeModelAsync(model, cancellationToken);
            checks.Add(!modelProbe.IsConfigured
                ? Warning("model", "模型服务", modelProbe.Summary, modelProbe.Detail)
                : modelProbe.Succeeded
                    ? Passed("model", "模型服务", modelProbe.Summary, modelProbe.Detail)
                    : Failed("model", "模型服务", modelProbe.Summary, modelProbe.Detail));
        }
        else
        {
            checks.Add(Skipped("model", "模型服务", "未执行模型探测"));
        }

        var sanitizedChecks = checks
            .Select(check => check with { Detail = Sanitize(check.Detail) })
            .ToArray();
        var succeeded = sanitizedChecks.All(check => check.Status != VerificationCheckStatus.Failed);
        Report(progress, InstallStep.Completed, 100, succeeded ? "安装后验证完成" : "安装后验证发现问题");
        return new VerificationReport(
            DateTimeOffset.Now,
            succeeded,
            sanitizedChecks,
            environment,
            gateway,
            modelProbe is null ? null : modelProbe with { Detail = Sanitize(modelProbe.Detail) });
    }

    private void AddEnvironmentChecks(List<VerificationCheck> checks, EnvironmentSnapshot snapshot)
    {
        if (!snapshot.IsSupportedWindows || !snapshot.Is64Bit)
        {
            checks.Add(Failed("environment", "Windows 环境", "当前系统不是受支持的 Windows x64", snapshot.WindowsVersion));
        }
        else
        {
            checks.Add(Passed("environment", "Windows 环境", $"{snapshot.WindowsVersion} / {snapshot.Architecture}"));
        }

        checks.Add(snapshot.HasCompatibleNode
            ? Passed("node", "Node.js", $"已发现兼容版本 {snapshot.NodeVersion}", snapshot.NodePath)
            : Failed("node", "Node.js", "未找到兼容的 Node.js", "需要 22.22.3+ 或 24.15+")
        );
        checks.Add(!string.IsNullOrWhiteSpace(snapshot.NpmVersion)
            ? Passed("npm", "npm", snapshot.NpmVersion, snapshot.NpmPath)
            : Failed("npm", "npm", "未找到 npm", snapshot.NpmPath));
        checks.Add(snapshot.FreeDiskGb <= 0 || snapshot.FreeDiskGb >= 2
            ? Passed("disk", "磁盘空间", snapshot.FreeDiskGb > 0 ? $"剩余 {snapshot.FreeDiskGb:0.0} GB" : "系统未返回可用空间")
            : Failed("disk", "磁盘空间", "系统盘剩余空间不足 2 GB"));
        checks.Add(snapshot.HasNetwork
            ? Passed("network", "网络连接", "基础网络可用")
            : Warning("network", "网络连接", "基础网络探测失败，模型或更新可能不可用"));
    }

    private string Sanitize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : _logs.Redact(value);

    private static string Combine(CommandResult result)
        => string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput, result.StandardError }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            .Trim();

    private static VerificationCheck Passed(string id, string name, string summary, string? detail = null)
        => new(id, name, VerificationCheckStatus.Passed, summary, detail);

    private static VerificationCheck Warning(string id, string name, string summary, string? detail = null)
        => new(id, name, VerificationCheckStatus.Warning, summary, detail);

    private static VerificationCheck Failed(string id, string name, string summary, string? detail = null)
        => new(id, name, VerificationCheckStatus.Failed, summary, detail);

    private static VerificationCheck Skipped(string id, string name, string summary)
        => new(id, name, VerificationCheckStatus.Skipped, summary);

    private static void Report(IProgress<InstallProgress>? progress, InstallStep step, int percent, string message)
        => progress?.Report(new InstallProgress(step, percent, message));
}
