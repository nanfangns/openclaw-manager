namespace OpenClawManager.Infrastructure;

public static class CommandCatalog
{
    public const string Node = "node";
    public const string Npm = "npm";
    public const string OpenClaw = "openclaw";

    public static IReadOnlyList<string> NpmInstallGlobal(string packageSpec)
        => new[] { "install", "--global", packageSpec };

    public static IReadOnlyList<string> OpenClawVersion()
        => new[] { "--version" };

    public static IReadOnlyList<string> OpenClawConfigValidate()
        => new[] { "config", "validate" };

    public static IReadOnlyList<string> OpenClawModelsList()
        => new[] { "models", "list" };

    public static IReadOnlyList<string> OpenClawGatewayInstall()
        => new[] { "gateway", "install" };

    public static IReadOnlyList<string> OpenClawGatewayStart()
        => new[] { "gateway", "start" };

    public static IReadOnlyList<string> OpenClawGatewayStop()
        => new[] { "gateway", "stop" };

    public static IReadOnlyList<string> OpenClawGatewayRestart()
        => new[] { "gateway", "restart" };

    public static IReadOnlyList<string> OpenClawGatewayUninstall()
        => new[] { "gateway", "uninstall" };

    public static IReadOnlyList<string> OpenClawGatewayStatusJson()
        => new[] { "gateway", "status", "--json" };
}
