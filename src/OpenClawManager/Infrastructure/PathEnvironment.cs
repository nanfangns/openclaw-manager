using System.IO;
using System.Security;

namespace OpenClawManager.Infrastructure;

public static class PathEnvironment
{
    public static string Merge(params string?[] values)
    {
        var entries = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(Path.PathSeparator, entries);
    }

    public static IReadOnlyList<string> GetProcessPathEntries()
        => Split(Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process));

    public static bool RefreshProcessPath()
    {
        try
        {
            var merged = Merge(
                Read("Path", EnvironmentVariableTarget.User),
                Read("Path", EnvironmentVariableTarget.Machine),
                Read("Path", EnvironmentVariableTarget.Process));
            if (merged.Length == 0)
            {
                return false;
            }

            Environment.SetEnvironmentVariable("Path", merged, EnvironmentVariableTarget.Process);
            return true;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? Read(string name, EnvironmentVariableTarget target)
    {
        try
        {
            return Environment.GetEnvironmentVariable(name, target);
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }
}
