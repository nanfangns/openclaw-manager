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
}
