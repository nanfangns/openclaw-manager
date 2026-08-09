using System.IO;

namespace OpenClawManager.Infrastructure;

public sealed class PathLayout
{
    public PathLayout(string? localAppData = null, string? userProfile = null)
    {
        LocalAppData = localAppData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        UserProfile = userProfile
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string LocalAppData { get; }
    public string UserProfile { get; }
    public string ManagerRoot => Path.Combine(LocalAppData, "OpenClawManager");
    public string LogsDirectory => Path.Combine(ManagerRoot, "logs");
    public string BackupsDirectory => Path.Combine(ManagerRoot, "backups");
    public string StateFile => Path.Combine(ManagerRoot, "state.json");
    public string OpenClawHome => Path.Combine(UserProfile, ".openclaw");
    public string OpenClawConfigFile => Path.Combine(OpenClawHome, "openclaw.json");

    public void EnsureDataDirectories()
    {
        Directory.CreateDirectory(ManagerRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
