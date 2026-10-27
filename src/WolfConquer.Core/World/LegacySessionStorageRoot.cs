namespace WolfConquer.Core.World;

// Syntax normalization is a denial preflight, never a persistence/UID authority.
internal static class LegacySessionStorageRoot
{
    internal static bool TryNormalize(string? root, out string? normalized)
    {
        normalized = null;
        if (root is null) return true; // Explicit storage-free session, not an empty provided path.
        if (string.IsNullOrWhiteSpace(root) || root.IndexOf('\0') >= 0 ||
            root.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
        try
        {
            // Windows normalization can erase a supplied trailing-dot/space alias.
            if (OperatingSystem.IsWindows() && !HasValidWindowsComponents(root)) return false;
            var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (OperatingSystem.IsWindows() && !HasValidWindowsComponents(candidate)) return false;
            normalized = candidate;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false; // Invalid supplied state remains rejected, never converted to allowed absence.
        }
    }

    private static bool HasValidWindowsComponents(string path)
    {
        path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.IndexOfAny(['*', '?']) >= 0) return false;
        var volume = Path.GetPathRoot(path);
        if (volume is null) return false;
        // UNC server/share labels belong to the volume, not DOS filesystem basenames.
        var parts = path[volume.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var part in parts)
        {
            if (part is "." or "..") continue; // Genuine navigation, not a suffix alias.
            // Reject device/wildcard/stream components and ambiguous Win32 suffix aliases.
            if (part.IndexOfAny(invalid) >= 0 || part.EndsWith(' ') || part.EndsWith('.') || IsReservedWindowsName(part)) return false;
        }
        return true;
    }

    private static bool IsReservedWindowsName(string part)
    {
        var dot = part.IndexOf('.');
        var stem = (dot < 0 ? part : part[..dot]).TrimEnd(' ');
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) return true;
        return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
            stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            (stem[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3');
    }
}
