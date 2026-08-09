using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class UninstallService : IUninstallService
{
    private static readonly Regex ProductCodePattern = new(@"\{[0-9A-Fa-f-]{36}\}", RegexOptions.Compiled);
    private readonly PathLayout _paths;
    private readonly IStateStore _stateStore;
    private readonly IOpenClawCliService _openClaw;
    private readonly IGatewayService _gateway;
    private readonly IConfigService _config;
    private readonly AdminElevation _elevation;
    private readonly ILogService _logs;

    public UninstallService(
        PathLayout paths,
        IStateStore stateStore,
        IOpenClawCliService openClaw,
        IGatewayService gateway,
        IConfigService config,
        AdminElevation elevation,
        ILogService logs)
    {
        _paths = paths;
        _stateStore = stateStore;
        _openClaw = openClaw;
        _gateway = gateway;
        _config = config;
        _elevation = elevation;
        _logs = logs;
    }

    public async Task<UninstallPreview> PreviewAsync(CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        return new UninstallPreview(
            state.GatewayInstalledByManager,
            state.OpenClawInstalledByManager,
            state.NodeInstalledByManager,
            Directory.Exists(_paths.OpenClawHome),
            state.OwnedShortcuts,
            BuildPreviewSummary(state));
    }

    public async Task<UninstallResult> ExecuteAsync(
        UninstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        try
        {
            if (options.RemoveOpenClaw && state.GatewayInstalledByManager && !options.RemoveGateway)
            {
                return Failure("OpenClaw 仍被已安装的 Gateway 使用，请同时勾选 Gateway 卸载。");
            }

            if (options.RemoveNode && ((state.OpenClawInstalledByManager && !options.RemoveOpenClaw)
                || (state.GatewayInstalledByManager && !options.RemoveGateway)))
            {
                return Failure("Node.js 仍被已安装的 OpenClaw/Gateway 使用，请先同时勾选相关资源卸载。");
            }

            if (options.RemoveGateway && state.GatewayInstalledByManager)
            {
                Report(progress, InstallStep.StartingGateway, 15, "正在停止 Gateway");
                var stop = await _gateway.StopAsync(cancellationToken);
                if (!stop.IsSuccess && !IsAlreadyStopped(stop.StandardError)) return Failure("停止 Gateway 失败");

                var removeGateway = await _gateway.UninstallAsync(cancellationToken);
                if (!removeGateway.IsSuccess) return Failure("卸载 Gateway 失败");
            }

            if (options.RemoveOpenClaw && state.OpenClawInstalledByManager)
            {
                Report(progress, InstallStep.InstallingOpenClaw, 40, "正在卸载 OpenClaw CLI");
                var removeOpenClaw = await _openClaw.UninstallAsync(cancellationToken);
                if (!removeOpenClaw.IsSuccess) return Failure("卸载 OpenClaw CLI 失败");
            }

            if (options.RemoveNode && state.NodeInstalledByManager)
            {
                Report(progress, InstallStep.InstallingNode, 55, "正在卸载由管理器安装的 Node.js");
                var removeNode = await UninstallOwnedNodeAsync(cancellationToken);
                if (!removeNode.IsSuccess) return Failure(removeNode.StandardError);
            }

            if (options.RemoveConfig && Directory.Exists(_paths.OpenClawHome))
            {
                Report(progress, InstallStep.BackingUpConfig, 70, "正在备份后删除 OpenClaw 配置");
                await _config.BackupAsync(cancellationToken);
                Directory.Delete(_paths.OpenClawHome, recursive: true);
            }

            if (options.RemoveWorkspace)
            {
                var workspace = Path.Combine(_paths.OpenClawHome, "workspace");
                if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
            }

            RemoveOwnedShortcuts(state.OwnedShortcuts);
            var updatedState = state with
            {
                NodeInstalledByManager = options.RemoveNode ? false : state.NodeInstalledByManager,
                OpenClawInstalledByManager = options.RemoveOpenClaw ? false : state.OpenClawInstalledByManager,
                GatewayInstalledByManager = options.RemoveGateway ? false : state.GatewayInstalledByManager,
                OwnedShortcuts = Array.Empty<string>(),
                CurrentStep = InstallStep.Completed
            };
            if (options.RemoveManagerData && Directory.Exists(_paths.ManagerRoot))
            {
                Directory.Delete(_paths.ManagerRoot, recursive: true);
            }
            else
            {
                await _stateStore.SaveAsync(updatedState, cancellationToken);
            }
            Report(progress, InstallStep.Completed, 100, "卸载操作完成");
            return new UninstallResult(true, "已按选择完成卸载；未勾选的资源已保留。");
        }
        catch (OperationCanceledException)
        {
            return new UninstallResult(false, "卸载操作已取消。");
        }
        catch (Exception ex)
        {
            _logs.Write(AppLogLevel.Error, "卸载操作发生异常", new Dictionary<string, string> { ["error"] = ex.Message });
            return Failure("卸载操作发生异常，请查看日志。");
        }
    }

    private async Task<CommandResult> UninstallOwnedNodeAsync(CancellationToken cancellationToken)
    {
        var productCode = FindNodeProductCode();
        if (productCode is null)
        {
            return new CommandResult(-1, string.Empty, "找不到由管理器安装的 Node.js MSI 产品信息，已停止卸载。", TimeSpan.Zero, false, false);
        }

        return await _elevation.RunAsync(
            "msiexec.exe",
            new[] { "/x", productCode, "/qn", "/norestart" },
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    private static string? FindNodeProductCode()
    {
        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        };
        foreach (var location in locations)
        {
            using var root = RegistryKey.OpenBaseKey(location.Item1, location.Item2);
            using var uninstall = root.OpenSubKey(location.Item3);
            if (uninstall is null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var item = uninstall.OpenSubKey(name);
                var displayName = item?.GetValue("DisplayName") as string;
                var windowsInstaller = item?.GetValue("WindowsInstaller");
                var uninstallString = item?.GetValue("UninstallString") as string;
                if (displayName?.StartsWith("Node.js", StringComparison.OrdinalIgnoreCase) != true
                    || windowsInstaller is null
                    || uninstallString is null) continue;
                var match = ProductCodePattern.Match(uninstallString);
                if (match.Success) return match.Value;
            }
        }

        return null;
    }

    private void RemoveOwnedShortcuts(IReadOnlyList<string> shortcuts)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _paths.DesktopShortcutPath,
            _paths.StartMenuShortcutPath
        };
        foreach (var shortcut in shortcuts.Where(path => allowed.Contains(path)))
        {
            if (File.Exists(shortcut)) File.Delete(shortcut);
        }
    }

    private string BuildPreviewSummary(InstallState state)
        => $"Gateway: {(state.GatewayInstalledByManager ? "将可卸载" : "未由管理器安装")}; "
           + $"OpenClaw: {(state.OpenClawInstalledByManager ? "将可卸载" : "未由管理器安装")}; "
           + $"Node.js: {(state.NodeInstalledByManager ? "可选择卸载" : "保留")}; "
           + $"配置: {(Directory.Exists(_paths.OpenClawHome) ? "存在，默认保留" : "不存在")}";

    private static bool IsAlreadyStopped(string error)
        => error.Contains("not running", StringComparison.OrdinalIgnoreCase)
            || error.Contains("未运行", StringComparison.OrdinalIgnoreCase);

    private UninstallResult Failure(string summary)
    {
        _logs.Write(AppLogLevel.Error, summary);
        return new UninstallResult(false, summary);
    }

    private static void Report(IProgress<InstallProgress> progress, InstallStep step, int percent, string message)
        => progress.Report(new InstallProgress(step, percent, message));
}
