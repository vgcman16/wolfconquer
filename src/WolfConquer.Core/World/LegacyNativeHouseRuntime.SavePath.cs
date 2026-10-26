using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHouseRuntime
{
    private static readonly StringComparer HousePathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // Bound once at genuine construction or validated file load. This is a save
    // location, not map/DynamicID permission or proof of a complete login lifetime.
    private readonly string? SourcePath;

    private static string? ConfiguredHousePath(uint uid)
    {
        try
        {
            var configured = LegacyNativeServerConfig.DbLocation;
            return string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured) ? null :
                Path.Combine(Path.GetFullPath(configured), "Houses", uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
        { return null; }
    }

    private static bool HouseSaveLeafExists(string path)
    {
        // The configured root and Houses directory must already be real ordinary
        // directories. A failed access/parent lookup is never treated as absence.
        for (var parent = Path.GetDirectoryName(path); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            var attributes = File.GetAttributes(parent);
            if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("houseSaveOrdinaryParentRequired");
        }
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("houseSaveOrdinaryLeafRequired");
            return true;
        }
        catch (FileNotFoundException) { return false; }
    }
}
