namespace WolfConquer.Core.World;

public static partial class LegacyOwnedAdmissionFixture
{
    internal static bool TryValidateTarget(string raw, out string destination, out string reason)
    {
        destination = string.Empty; reason = "ownedAdmissionFixtureTargetInvalid";
        try
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 4096 || !Path.IsPathFullyQualified(raw) ||
                raw.StartsWith(@"\\", StringComparison.Ordinal) || raw.IndexOfAny(['\r', '\n', '\0']) >= 0) return false;
            var parts = raw.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(part => part is "." or ".." || OperatingSystem.IsWindows() &&
                    (part.EndsWith(' ') || part.EndsWith('.')))) return false;
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            var name = Path.GetFileName(full);
            if (!name.StartsWith(DirectoryPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[DirectoryPrefix.Length..], "N", out var id) || id == Guid.Empty ||
                name != DirectoryPrefix + id.ToString("N")) return false;
            var volume = Path.GetPathRoot(full)!;
            if (Path.GetRelativePath(volume, full).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries).Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return false;
            var parent = Path.GetDirectoryName(full);
            if (parent is null || !Directory.Exists(parent)) return false;
            for (DirectoryInfo? current = new(parent); current is not null; current = current.Parent)
            {
                if (IsProtectedDirectory(current.FullName))
                { reason = "ownedAdmissionFixtureProtectedTarget"; return false; }
                // Never place a synthetic fixture inside an existing save tree.
                // This is a metadata-only shape check, not a data import/scan.
                if (Directory.Exists(Path.Combine(current.FullName, "Users")) &&
                    Directory.Exists(Path.Combine(current.FullName, "PlayersItems")))
                { reason = "ownedAdmissionFixtureProtectedSaveTree"; return false; }
            }
            RequirePhysicalDirectoryChain(parent);
            RequireAbsent(full);
            RequireAbsent(Path.Combine(parent, "." + name + ".staging"));
            destination = full; reason = string.Empty; return true;
        }
        catch (Exception error) when (IsFixtureFailure(error))
        { reason = "ownedAdmissionFixtureTargetRefused:" + error.GetType().Name; return false; }
    }

    private static bool IsProtectedDirectory(string path)
    {
        foreach (var folder in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonProgramFiles, Environment.SpecialFolder.CommonProgramFilesX86,
                     Environment.SpecialFolder.CommonApplicationData })
        {
            var protectedPath = Environment.GetFolderPath(folder);
            if (protectedPath.Length != 0 && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedPath)),
                    path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static void RequireAbsent(string path)
    {
        try { _ = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        throw new IOException("The synthetic fixture target already exists.");
    }

    private static void RequirePhysicalDirectoryChain(string path)
    {
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
        {
            var attributes = File.GetAttributes(directory.FullName);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                throw new IOException("Synthetic fixture directories cannot traverse reparse points.");
        }
    }
}
