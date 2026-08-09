namespace OpenClawManager.Infrastructure;

public static class VersionPolicy
{
    public static readonly Version Node22Minimum = new(22, 22, 3);
    public static readonly Version Node24Minimum = new(24, 15, 0);
    public static readonly Version Node25Minimum = new(25, 9, 0);

    public static bool IsNodeSupported(string? value)
    {
        if (!TryParse(value, out var version))
        {
            return false;
        }

        return version.Major switch
        {
            22 => version >= Node22Minimum,
            24 => version >= Node24Minimum,
            >= 25 => version >= Node25Minimum,
            _ => false
        };
    }

    public static bool TryParse(string? value, out Version version)
    {
        var normalized = value?.Trim().TrimStart('v');
        return Version.TryParse(normalized, out version!);
    }
}
