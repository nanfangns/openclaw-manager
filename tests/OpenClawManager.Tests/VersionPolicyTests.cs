using OpenClawManager.Infrastructure;

namespace OpenClawManager.Tests;

public sealed class VersionPolicyTests
{
    [Theory]
    [InlineData("v22.22.3", true)]
    [InlineData("v22.22.2", false)]
    [InlineData("v24.15.0", true)]
    [InlineData("v24.14.9", false)]
    [InlineData("v25.9.0", true)]
    [InlineData("v21.99.0", false)]
    [InlineData(null, false)]
    public void Checks_current_node_runtime_floor(string? value, bool expected)
    {
        Assert.Equal(expected, VersionPolicy.IsNodeSupported(value));
    }
}
