namespace WolfConquer.Updates.ClientLayout;

public static partial class ClientAssetLayout
{
    private static string CanonicalRoot(string root)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The DX8 layout requires Windows NTFS junctions.");
        if (string.IsNullOrWhiteSpace(root) || root.Length is < 4 or > 2048 || !char.IsAsciiLetter(root[0]) ||
            root[1] != ':' || root[2] != '\\' || root.Contains('/') || !Path.IsPathFullyQualified(root))
            throw new InvalidDataException("Asset layout requires an explicit local absolute root.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!full.Equals(Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase) ||
            full.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Asset layout root must be a canonical non-volume directory.");
        foreach (var segment in full[3..].Split('\\'))
        {
            var stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (segment.Length is 0 or > 255 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.Any(character => char.IsControl(character) || ":<>\"|?*".Contains(character)) ||
                stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$" ||
                ((stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    stem.Length == 4 && "123456789¹²³".Contains(stem[3])))
                throw new InvalidDataException("Asset layout root contains an unsafe Windows directory alias.");
        }
        return full;
    }

    private static string Preflight(string root, bool requireDirectories)
    {
        root = CanonicalRoot(root);
        CheckOrdinaryDirectory(root, requireDirectories);
        var engine = Path.Combine(root, EngineDirectory);
        bool engineExists = CheckOrdinaryDirectory(engine, requireDirectories);
        foreach (var name in Directories)
        {
            var target = Path.Combine(root, name);
            var alias = Path.Combine(engine, name);
            bool targetExists = CheckOrdinaryDirectory(target, requireDirectories);
            if (engineExists && targetExists)
                ClientDirectoryJunction.IsExact(alias, target); // Also validates ordinary pinned parents when the leaf is absent.
            else if (Attributes(alias) is not null)
                throw new InvalidDataException("A DX8 asset alias has no ordinary root target.");
        }
        return root;
    }

    private static bool CheckOrdinaryDirectory(string path, bool required)
    {
        bool exists = false;
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            var attributes = Attributes(current);
            if (attributes is { } value)
            {
                if ((value & FileAttributes.ReparsePoint) != 0 || (value & FileAttributes.Directory) == 0)
                    throw new InvalidDataException("Client asset targets and ancestors must be ordinary directories.");
                if (current == path) exists = true;
            }
            var parent = Path.GetDirectoryName(current);
            if (parent is not null && Directory.Exists(parent))
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.EnumerateFileSystemEntries(parent).Any(entry => !names.Add(Path.GetFileName(entry))))
                    throw new InvalidDataException("Existing client path names collide under Windows comparison.");
            }
        }
        if (required && !exists) throw new DirectoryNotFoundException("A signed DX8 layout directory is absent: " + path);
        return exists;
    }

    private static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void CheckOwnedPaths(string root, string name)
    {
        CheckOrdinaryDirectory(root, required: true);
        CheckOrdinaryDirectory(Path.Combine(root, EngineDirectory), required: true);
        CheckOrdinaryDirectory(Path.Combine(root, name), required: true);
    }
}
