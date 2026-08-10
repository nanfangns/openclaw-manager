using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public sealed class InstallCoordinator : IInstallCoordinator
{
    private readonly IEnvironmentService _environment;
    private readonly INodeService _node;
    private readonly IOpenClawCliService _openClaw;
    private readonly IConfigService _config;
    private readonly IGatewayService _gateway;
    private readonly IStateStore _stateStore;
    private readonly ILogService _logs;
    private readonly IInstallationVerifier _verifier;

    public InstallCoordinator(
        IEnvironmentService environment,
        INodeService node,
        IOpenClawCliService openClaw,
        IConfigService config,
        IGatewayService gateway,
        IStateStore stateStore,
        ILogService logs,
        IInstallationVerifier? verifier = null)
    {
        _environment = environment;
        _node = node;
        _openClaw = openClaw;
        _config = config;
        _gateway = gateway;
        _stateStore = stateStore;
        _logs = logs;
        _verifier = verifier ?? new InstallationVerifier(environment, openClaw, config, gateway, logs);
    }

    public InstallWorkflowState Current { get; private set; } =
        new(InstallStep.NotStarted, 0, "尚未开始", false, false, false);

    public async Task<InstallWorkflowResult> RunAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        if (Current.IsRunning)
        {
            return new InstallWorkflowResult(false, Current.Step, "已有安装任务正在运行。");
        }

        Current = Current with { IsRunning = true, IsCompleted = false, IsFailed = false };
        try
        {
            var installedOpenClaw = false;
            Report(progress, InstallStep.Detecting, 5, "正在检查系统环境");
            var environment = await _environment.DetectAsync(cancellationToken);
            if (!environment.IsSupportedWindows || !environment.Is64Bit)
            {
                return Fail(progress, InstallStep.Detecting, "当前系统不是受支持的 Windows x64。");
            }

            if (environment.FreeDiskGb > 0 && environment.FreeDiskGb < 2)
            {
                return Fail(progress, InstallStep.Detecting, "系统盘剩余空间不足 2 GB。");
            }

            Report(progress, InstallStep.InstallingNode, 15, "正在准备 Node.js");
            if (environment.GatewayPortInUse)
            {
                var existingGateway = await _gateway.GetStatusAsync(cancellationToken);
                if (!existingGateway.IsHealthy)
                {
                    return Fail(progress, InstallStep.Detecting, $"Gateway 默认端口 {environment.GatewayPort} 已被其他进程占用。");
                }
            }

            var node = await _node.EnsureCompatibleAsync(
                new NodeInstallOptions(options.InstallNodeIfMissing),
                progress,
                cancellationToken);
            if (!node.Succeeded)
            {
                return Fail(progress, InstallStep.InstallingNode, node.Summary);
            }

            Report(progress, InstallStep.VerifyingNode, 30, "正在验证 OpenClaw 运行环境");
            var openClaw = await _openClaw.GetVersionAsync(cancellationToken);
            if (!openClaw.Succeeded)
            {
                Report(progress, InstallStep.InstallingOpenClaw, 36, "正在安装 OpenClaw");
                var install = await _openClaw.InstallAsync("openclaw@latest", cancellationToken);
                if (!install.IsSuccess)
                {
                    return Fail(progress, InstallStep.InstallingOpenClaw, "OpenClaw 安装失败。");
                }

                installedOpenClaw = true;
                openClaw = await _openClaw.GetVersionAsync(cancellationToken);
                if (!openClaw.Succeeded)
                {
                    return Fail(progress, InstallStep.VerifyingOpenClaw, "OpenClaw 安装后验证失败。");
                }
            }

            Report(progress, InstallStep.BackingUpConfig, 52, "正在备份现有 OpenClaw 配置");
            var backupPath = await _config.BackupAsync(cancellationToken);

            if (options.Model is not null)
            {
                Report(progress, InstallStep.ConfiguringModel, 62, "正在配置模型提供商");
                var configResult = await _config.ConfigureModelAsync(options.Model, cancellationToken);
                if (!configResult.IsSuccess)
                {
                    await _config.RestoreAsync(backupPath, cancellationToken);
                    await PersistStateAsync(
                        node,
                        openClaw,
                        installedOpenClaw,
                        false,
                        backupPath,
                        InstallStep.ConfiguringModel,
                        cancellationToken);
                    return Fail(progress, InstallStep.ConfiguringModel, "模型配置失败，已恢复原配置。");
                }
            }

            if (options.InstallGateway)
            {
                Report(progress, InstallStep.InstallingGateway, 72, "正在安装 Gateway 服务");
                var gatewayInstall = await _gateway.InstallAsync(cancellationToken);
                if (!gatewayInstall.IsSuccess)
                {
                    return Fail(progress, InstallStep.InstallingGateway, "Gateway 服务安装失败。");
                }

                await PersistStateAsync(
                    node,
                    openClaw,
                    installedOpenClaw,
                    true,
                    backupPath,
                    InstallStep.InstallingGateway,
                    cancellationToken);

                Report(progress, InstallStep.StartingGateway, 82, "正在启动 Gateway");
                var start = await _gateway.StartAsync(cancellationToken);
                if (!start.IsSuccess)
                {
                    return Fail(progress, InstallStep.StartingGateway, "Gateway 启动失败。");
                }

                Report(progress, InstallStep.HealthChecking, 92, "正在检查 Gateway 健康状态");
                var status = await _gateway.GetStatusAsync(cancellationToken);
                if (!status.IsHealthy)
                {
                    return Fail(progress, InstallStep.HealthChecking, status.Summary);
                }
            }

            Report(progress, InstallStep.HealthChecking, 96, "正在执行安装后完整验证");
            var verification = await _verifier.VerifyAsync(
                options.Model,
                options.InstallGateway,
                options.Model is not null,
                progress,
                cancellationToken);
            if (!verification.Succeeded)
            {
                await PersistStateAsync(
                    node,
                    openClaw,
                    installedOpenClaw,
                    options.InstallGateway,
                    backupPath,
                    InstallStep.HealthChecking,
                    cancellationToken);
                return Fail(progress, InstallStep.HealthChecking, $"安装后验证失败：{verification.Summary}");
            }

            await PersistStateAsync(
                node,
                openClaw,
                installedOpenClaw,
                options.InstallGateway,
                backupPath,
                InstallStep.Completed,
                cancellationToken);

            Report(progress, InstallStep.Completed, 100, "安装完成");
            Current = new InstallWorkflowState(InstallStep.Completed, 100, "安装完成", false, true, false);
            return new InstallWorkflowResult(true, InstallStep.Completed, "OpenClaw 安装并启动成功。");
        }
        catch (OperationCanceledException)
        {
            Current = new InstallWorkflowState(InstallStep.Cancelled, Current.Percent, "用户取消了操作", false, false, true);
            progress.Report(new InstallProgress(InstallStep.Cancelled, Current.Percent, "用户取消了操作", true));
            return new InstallWorkflowResult(false, InstallStep.Cancelled, "操作已取消。");
        }
        catch (Exception ex)
        {
            _logs.Write(AppLogLevel.Error, "安装流程发生未处理异常", new Dictionary<string, string>
            {
                ["error"] = ex.Message
            });
            return Fail(progress, Current.Step, "安装流程发生异常，请查看日志。");
        }
        finally
        {
            if (Current.IsRunning)
            {
                Current = Current with { IsRunning = false };
            }
        }
    }

    public async Task<InstallWorkflowResult> RepairAsync(
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        Report(progress, InstallStep.HealthChecking, 20, "正在检查 OpenClaw 配置");
        var validation = await _config.ValidateAsync(cancellationToken);
        if (!validation.IsSuccess)
        {
            return Fail(progress, InstallStep.HealthChecking, "OpenClaw 配置校验失败。");
        }

        var status = await _gateway.GetStatusAsync(cancellationToken);
        if (!status.IsRunning)
        {
            Report(progress, InstallStep.StartingGateway, 60, "Gateway 未运行，正在启动");
            var start = await _gateway.StartAsync(cancellationToken);
            if (!start.IsSuccess)
            {
                return Fail(progress, InstallStep.StartingGateway, "Gateway 修复启动失败。");
            }
        }

        var after = await _gateway.GetStatusAsync(cancellationToken);
        if (!after.IsHealthy)
        {
            return Fail(progress, InstallStep.HealthChecking, "Gateway 修复后健康检查失败。");
        }

        Report(progress, InstallStep.Completed, 100, "修复完成");
        Current = new InstallWorkflowState(InstallStep.Completed, 100, "修复完成", false, true, false);
        return new InstallWorkflowResult(true, InstallStep.Completed, "OpenClaw 环境修复完成。");
    }

    private void Report(IProgress<InstallProgress> progress, InstallStep step, int percent, string message)
    {
        Current = new InstallWorkflowState(step, percent, message, true, false, false);
        progress.Report(new InstallProgress(step, percent, message));
        _logs.Write(AppLogLevel.Information, message, new Dictionary<string, string>
        {
            ["step"] = step.ToString(),
            ["percent"] = percent.ToString()
        });
    }

    private async Task PersistStateAsync(
        NodeResult node,
        OpenClawVersionResult openClaw,
        bool installedOpenClaw,
        bool installedGateway,
        string backupPath,
        InstallStep step,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await _stateStore.LoadAsync(cancellationToken);
            var backups = state.ConfigBackups.Contains(backupPath, StringComparer.OrdinalIgnoreCase)
                ? state.ConfigBackups
                : state.ConfigBackups.Append(backupPath).ToArray();
            await _stateStore.SaveAsync(state with
            {
                NodeVersion = node.Version ?? state.NodeVersion,
                OpenClawVersion = openClaw.Version ?? state.OpenClawVersion,
                NodeInstalledByManager = state.NodeInstalledByManager || node.InstalledByManager,
                OpenClawInstalledByManager = state.OpenClawInstalledByManager || installedOpenClaw,
                GatewayInstalledByManager = state.GatewayInstalledByManager || installedGateway,
                ConfigBackups = backups,
                CurrentStep = step
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logs.Write(AppLogLevel.Warning, "保存安装状态失败", new Dictionary<string, string>
            {
                ["error"] = ex.Message,
                ["step"] = step.ToString()
            });
        }
    }

    private InstallWorkflowResult Fail(IProgress<InstallProgress> progress, InstallStep step, string message)
    {
        Current = new InstallWorkflowState(step, Current.Percent, message, false, false, true);
        progress.Report(new InstallProgress(step, Current.Percent, message, true));
        _logs.Write(AppLogLevel.Error, message);
        return new InstallWorkflowResult(false, step, message);
    }
}
