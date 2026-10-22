namespace WolfConquer.Updates.Manifest;

// Lexical Windows-target policy, independent of the host OS. The installer must
// additionally resolve containment and reject existing reparse-point ancestors.
public static class UpdatePathPolicy
{
    private static readonly HashSet<string> ProtectedRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "wolfconquer-updates.json", "trusted-release-keys.json", "WolfConquer.Updater.exe",
        "WolfConquer.Updater.dll", "WolfConquer.Updater.deps.json", "WolfConquer.Updater.runtimeconfig.json",
    };

    public static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 1024 || path[0] == '/' ||
            path.Any(character => char.IsControl(character) || "\\:<>\"|?*".Contains(character)))
            throw new InvalidDataException("Update path is not a safe relative Windows file path.");
        string[] segments = path.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length is 0 or > 255 || segment is "." or ".." ||
                segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".wolfconquer-updates", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update path contains a forbidden segment.");
            string stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                                      stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 "123456789\u00b9\u00b2\u00b3".Contains(stem[3])))
                throw new InvalidDataException("Update path contains a reserved Windows device name.");
        }
        if (ProtectedRootFiles.Contains(segments[0]))
            throw new InvalidDataException("The update cannot replace updater trust or executable files.");
    }
}
