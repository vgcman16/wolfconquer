internal static partial class Program
{
    private static string ConfigDecodeFullPath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Decoder paths must be explicit absolute local paths.");
        var root = Path.GetPathRoot(path)!;
        foreach (var segment in path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]))
            if (segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl) ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                (OperatingSystem.IsWindows() && (segment.EndsWith(' ') || segment.EndsWith('.'))))
                throw new ArgumentException("Decoder paths must use ordinary path components.");
        return Path.GetFullPath(path);
    }

    private static void ValidateConfigDecodeRoles(ConfigDecodeOptions options)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(options.Input, options.Output, comparison) ||
            string.Equals(options.PublicData, options.Output, comparison) ||
            string.Equals(options.Input, options.PublicData, comparison))
            throw new ArgumentException("Input, public-data and new-output roles must be distinct.");
        ValidateConfigDecodeInput(options.Input);
        ValidateConfigDecodeInput(options.PublicData);
        ValidateConfigDecodeOutput(options.Output);
    }

    private static void ValidateConfigDecodeInput(string path)
    {
        ValidateConfigDecodeParents(path);
        var file = new FileInfo(path);
        file.Refresh();
        if (!file.Exists || file.LinkTarget is not null ||
            (file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
            throw new IOException("Decoder input must be an existing ordinary nonlinked file.");
        RequireConfigDecodeOrdinaryPath(path);
    }

    private static void ValidateConfigDecodeOutput(string path)
    {
        ValidateConfigDecodeParents(path);
        if (new FileInfo(path).LinkTarget is not null)
            throw new IOException("Decoder output must not be linked.");
        try { _ = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        throw new IOException("Decoder output already exists.");
    }

    private static void ValidateConfigDecodeParents(string path)
    {
        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (!parent.Exists || parent.LinkTarget is not null ||
                (parent.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new IOException("Decoder ancestors must be existing ordinary nonlinked directories.");
        }
    }
}
