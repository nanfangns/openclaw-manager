using System.Windows;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new PathLayout();
        var runner = new ProcessRunner();
        var logs = new LogService(paths);
        var stateStore = new JsonStateStore(paths);
        var credentials = new CredentialService();
        var environment = new EnvironmentService(runner);
        var elevation = new AdminElevation();
        var node = new NodeService(environment, runner, logs, elevation);
        var openClaw = new OpenClawCliService(runner, logs);
        var config = new ConfigService(paths, runner, credentials, logs);
        var gateway = new GatewayService(runner);
        var coordinator = new InstallCoordinator(environment, node, openClaw, config, gateway, stateStore, logs);
        var uninstall = new UninstallService(paths, stateStore, openClaw, gateway, config, elevation, logs);

        MainWindow = new MainWindow(environment, config, gateway, coordinator, uninstall, logs);
        MainWindow.Show();
    }
}
