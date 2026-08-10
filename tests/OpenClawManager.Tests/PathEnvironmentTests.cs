using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class PathEnvironmentTests
{
    [Fact]
    public void Merge_keeps_new_machine_entries_and_removes_duplicates()
    {
        var path = PathEnvironment.Merge(
            @"C:\Users\tester\AppData\Roaming\npm;C:\Windows\System32",
            @"C:\Program Files\nodejs;C:\Windows\System32",
            @"C:\Tools");

        Assert.Equal(
            string.Join(Path.PathSeparator, new[]
            {
                @"C:\Users\tester\AppData\Roaming\npm",
                @"C:\Windows\System32",
                @"C:\Program Files\nodejs",
                @"C:\Tools"
            }),
            path);
    }

    [Fact]
    public void Merge_ignores_empty_entries()
    {
        Assert.Equal(@"C:\node", PathEnvironment.Merge(null, "", @"C:\node;;"));
    }
}
