using OpenClawManager.Core.Models;
using OpenClawManager.Core.Services;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class StateStoreTests
{
    [Fact]
    public async Task Missing_state_returns_empty_state()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);
        var store = new JsonStateStore(paths);

        var state = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(InstallStep.NotStarted, state.CurrentStep);
        Assert.False(File.Exists(paths.StateFile));
    }

    [Fact]
    public async Task Saved_state_round_trips_atomically()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);
        var store = new JsonStateStore(paths);
        var expected = InstallState.Empty with
        {
            NodeVersion = "v24.15.0",
            OpenClawVersion = "2026.7.1",
            CurrentStep = InstallStep.Completed,
            NodeInstalledByManager = true
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.NodeVersion, actual.NodeVersion);
        Assert.Equal(expected.OpenClawVersion, actual.OpenClawVersion);
        Assert.Equal(expected.NodeInstalledByManager, actual.NodeInstalledByManager);
        Assert.Equal(expected.CurrentStep, actual.CurrentStep);
        Assert.Equal(expected.OwnedShortcuts, actual.OwnedShortcuts);
        Assert.True(File.Exists(paths.StateFile));
        Assert.False(File.Exists(paths.StateFile + ".tmp"));
    }

    [Fact]
    public void Path_layout_keeps_manager_data_separate_from_legacy_home()
    {
        using var temp = new TemporaryDirectory();
        var paths = new PathLayout(temp.Path, temp.Path);

        Assert.StartsWith(temp.Path, paths.ManagerRoot, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(paths.ManagerRoot, paths.OpenClawHome);
        Assert.EndsWith("OpenClawManager", paths.ManagerRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "oc-manager-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
