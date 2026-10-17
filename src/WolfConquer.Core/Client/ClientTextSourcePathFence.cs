namespace WolfConquer.Core.Client;

public static class ClientTextSourcePathFence
{
    private static readonly string[] BlockedWrongGamePathSegments =
    [
        "Destroyer",
        "Destroyer Online",
        "Destroyer-Online",
        "Demon Online",
        "Demon-Online",
        "DemonOnline",
        "Eudemons",
        "Eudemons Online",
        "Eudemons-Online",
        "EudemonsOnline",
        "Eudemons-derived",
    ];

    public static bool IsAllowedSourcePath(string path)
    {
        return !IsBlockedWrongGameSourcePath(path);
    }

    public static bool IsBlockedWrongGameSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        foreach (var segment in fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (BlockedWrongGamePathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
