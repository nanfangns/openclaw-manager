using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager;

public partial class MainWindow : Window
{
    private readonly IEnvironmentService _environment;
    private readonly IConfigService _config;
    private readonly IGatewayService _gateway;
    private readonly IInstallCoordinator _coordinator;
    private readonly IUninstallService _uninstall;
    private readonly LogService _logs;
    private readonly Brush _successBrush;
    private readonly Brush _warningBrush;
    private readonly Brush _dangerBrush;

    public MainWindow(IEnvironmentService environment, IConfigService config, IGatewayService gateway, IInstallCoordinator coordinator, IUninstallService uninstall, LogService logs)
    {
        InitializeComponent();
        _environment = environment;
        _config = config;
        _gateway = gateway;
        _coordinator = coordinator;
        _uninstall = uninstall;
        _logs = logs;
        _successBrush = (Brush)FindResource("SuccessBrush");
        _warningBrush = (Brush)FindResource("WarningBrush");
        _dangerBrush = (Brush)FindResource("DangerBrush");
        ProviderCombo.ItemsSource = ModelProviderCatalog.All;
        ProviderCombo.SelectedIndex = 0;
        _logs.EntryAdded += Logs_EntryAdded;
        Loaded += async (_, _) => await RefreshAllAsync();
    }

    private void Overview_Click(object sender, RoutedEventArgs e) => ShowPage(OverviewPage, "概览", "查看本机 OpenClaw 安装与服务状态");
    private void Install_Click(object sender, RoutedEventArgs e) => ShowPage(InstallPage, "安装与配置", "在线安装运行环境、OpenClaw CLI 和 Gateway");
    private async void Gateway_Click(object sender, RoutedEventArgs e) { ShowPage(GatewayPage, "Gateway", "管理本机 Gateway 服务生命周期"); await RefreshGatewayAsync(); }
    private async void Model_Click(object sender, RoutedEventArgs e) { ShowPage(ModelPage, "模型与备份", "配置模型凭据并安全管理 OpenClaw 配置备份"); await RefreshBackupsAsync(); }
    private void Logs_Click(object sender, RoutedEventArgs e) => ShowPage(LogsPage, "运行日志", "查看操作记录（敏感值已脱敏）");
    private async void Uninstall_Click(object sender, RoutedEventArgs e) { ShowPage(UninstallPage, "安全卸载", "按资源归属和明确选择清理 OpenClaw"); await PreviewUninstallAsync(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

    private async Task RefreshAllAsync()
    {
        try
        {
            HeaderStatusText.Text = "正在检测";
            HeaderStatusDot.Fill = _warningBrush;
            var snapshot = await _environment.DetectAsync(CancellationToken.None);
            OpenClawVersionText.Text = snapshot.OpenClawVersion ?? "未安装";
            OpenClawPathText.Text = snapshot.OpenClawPath ?? "未找到 openclaw 命令";
            NodeVersionText.Text = snapshot.NodeVersion ?? "未安装";
            NodePathText.Text = snapshot.HasCompatibleNode ? "版本兼容" : "需要 22.22.3+ / 24.15+";
            await RefreshGatewayAsync();
            HeaderStatusText.Text = snapshot.OpenClawVersion is null ? "尚未安装" : "已检测到 OpenClaw";
            HeaderStatusDot.Fill = snapshot.OpenClawVersion is null ? _warningBrush : _successBrush;
        }
        catch (Exception ex)
        {
            SetHeaderError(ex.Message);
        }
    }

    private async Task RefreshGatewayAsync()
    {
        try
        {
            var status = await _gateway.GetStatusAsync(CancellationToken.None);
            var text = status.IsHealthy ? "运行中" : status.IsInstalled ? "已安装，未运行" : "未安装";
            GatewayStatusText.Text = text;
            GatewayLargeStatusText.Text = text;
            GatewayDetailText.Text = $"{status.Summary}  |  端口 {status.Port}";
            GatewayLargeStatusText.Foreground = status.IsHealthy ? _successBrush : _warningBrush;
        }
        catch (Exception ex)
        {
            GatewayStatusText.Text = "读取失败";
            GatewayLargeStatusText.Text = "读取失败";
            GatewayDetailText.Text = ex.Message;
        }
    }

    private async void RunInstall_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigureModelCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            MessageBox.Show("启用模型配置时必须填写 API Key。", "需要 API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var provider = ProviderCombo.SelectedItem as ModelProvider;
        var model = ConfigureModelCheckBox.IsChecked == true && provider is not null
            ? new ModelConfiguration(provider.Id, ModelIdBox.Text.Trim(), ApiKeyBox.Password, BaseUrlBox.Text.Trim())
            : null;
        var options = new InstallOptions(model, InstallNodeCheckBox.IsChecked == true, InstallGatewayCheckBox.IsChecked == true);
        SetOperationRunning(true);
        try
        {
            var progress = new Progress<InstallProgress>(item =>
            {
                InstallProgressBar.Value = item.Percent;
                InstallProgressText.Text = item.Message;
                InstallProgressText.Foreground = item.IsError ? _dangerBrush : _warningBrush;
            });
            var result = await _coordinator.RunAsync(options, progress, CancellationToken.None);
            InstallProgressText.Text = result.Summary;
            InstallProgressText.Foreground = result.Succeeded ? _successBrush : _dangerBrush;
            await RefreshAllAsync();
            if (result.Succeeded) MessageBox.Show(result.Summary, "安装完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            InstallProgressText.Text = $"失败：{ex.Message}";
            InstallProgressText.Foreground = _dangerBrush;
        }
        finally
        {
            SetOperationRunning(false);
        }
    }

    private async void Repair_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetOperationRunning(true);
            var progress = new Progress<InstallProgress>(item => InstallProgressText.Text = item.Message);
            var result = await _coordinator.RepairAsync(progress, CancellationToken.None);
            await RefreshAllAsync();
            MessageBox.Show(result.Summary, result.Succeeded ? "修复完成" : "修复失败", MessageBoxButton.OK, result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            SetOperationRunning(false);
        }
    }

    private async void StartGateway_Click(object sender, RoutedEventArgs e) => await GatewayActionAsync(() => _gateway.StartAsync(CancellationToken.None));
    private async void StopGateway_Click(object sender, RoutedEventArgs e) => await GatewayActionAsync(() => _gateway.StopAsync(CancellationToken.None));
    private async void RestartGateway_Click(object sender, RoutedEventArgs e) => await GatewayActionAsync(() => _gateway.RestartAsync(CancellationToken.None));

    private async Task GatewayActionAsync(Func<Task<CommandResult>> action)
    {
        try
        {
            SetOperationRunning(true);
            var result = await action();
            if (!result.IsSuccess) MessageBox.Show(result.StandardError, "Gateway 操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Gateway 操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetOperationRunning(false);
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await _config.BackupAsync(CancellationToken.None);
            await RefreshBackupsAsync();
            BackupStatusText.Text = $"已创建备份：{path}";
        }
        catch (Exception ex) { BackupStatusText.Text = $"备份失败：{ex.Message}"; }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupCombo.SelectedItem is not ConfigBackup backup) { BackupStatusText.Text = "请先选择一个备份。"; return; }
        if (MessageBox.Show("恢复会覆盖当前 .openclaw 配置文件，是否继续？", "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await _config.RestoreAsync(backup.Path, CancellationToken.None); BackupStatusText.Text = $"已恢复：{backup.Path}"; }
        catch (Exception ex) { BackupStatusText.Text = $"恢复失败：{ex.Message}"; }
    }

    private async Task RefreshBackupsAsync()
    {
        var backups = await _config.ListBackupsAsync(CancellationToken.None);
        BackupCombo.ItemsSource = backups;
        BackupStatusText.Text = backups.Count == 0 ? "尚无备份" : $"共 {backups.Count} 个备份";
    }

    private async void PreviewUninstall_Click(object sender, RoutedEventArgs e) => await PreviewUninstallAsync();

    private async Task PreviewUninstallAsync()
    {
        try
        {
            var preview = await _uninstall.PreviewAsync(CancellationToken.None);
            UninstallPreviewText.Text = preview.Summary;
        }
        catch (Exception ex)
        {
            UninstallPreviewText.Text = $"预览失败：{ex.Message}";
        }
    }

    private async void RunUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("将按当前勾选项执行清理，未勾选资源会保留。继续？", "确认安全卸载", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var options = new UninstallOptions(
            RemoveOpenClawCheckBox.IsChecked == true,
            RemoveNodeCheckBox.IsChecked == true,
            RemoveConfigCheckBox.IsChecked == true,
            RemoveWorkspaceCheckBox.IsChecked == true,
            RemoveGatewayCheckBox.IsChecked == true,
            RemoveManagerDataCheckBox.IsChecked == true);
        try
        {
            SetOperationRunning(true);
            var progress = new Progress<InstallProgress>(item => UninstallStatusText.Text = item.Message);
            var result = await _uninstall.ExecuteAsync(options, progress, CancellationToken.None);
            UninstallStatusText.Text = result.Summary;
            UninstallStatusText.Foreground = result.Succeeded ? _successBrush : _dangerBrush;
            await PreviewUninstallAsync();
        }
        catch (Exception ex)
        {
            UninstallStatusText.Text = $"卸载失败：{ex.Message}";
            UninstallStatusText.Foreground = _dangerBrush;
        }
        finally
        {
            SetOperationRunning(false);
        }
    }

    private void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var showBaseUrl = (ProviderCombo.SelectedItem as ModelProvider)?.RequiresBaseUrl == true;
        BaseUrlLabel.Visibility = showBaseUrl ? Visibility.Visible : Visibility.Collapsed;
        BaseUrlBox.Visibility = showBaseUrl ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var path = new PathLayout().OpenClawHome;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => LogList.Items.Clear();

    private void Logs_EntryAdded(object? sender, LogEntry entry)
        => Dispatcher.BeginInvoke(() => LogList.Items.Insert(0, $"{entry.Timestamp:HH:mm:ss}  [{entry.Level}]  {entry.Message}"));

    private void ShowPage(Grid page, string title, string subtitle)
    {
        OverviewPage.Visibility = Visibility.Collapsed;
        InstallPage.Visibility = Visibility.Collapsed;
        GatewayPage.Visibility = Visibility.Collapsed;
        ModelPage.Visibility = Visibility.Collapsed;
        LogsPage.Visibility = Visibility.Collapsed;
        UninstallPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;
    }

    private void SetOperationRunning(bool running)
    {
        InstallButton.IsEnabled = !running;
        Cursor = running ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private void SetHeaderError(string message)
    {
        HeaderStatusText.Text = "检测失败";
        HeaderStatusDot.Fill = _dangerBrush;
        _logs.Write(AppLogLevel.Error, "状态检测失败", new Dictionary<string, string> { ["error"] = message });
    }
}
