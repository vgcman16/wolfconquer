namespace WolfConquer.Core.Client;

public static partial class ChineseFullClientCatalogIntake
{
    /// <summary>Fixed ordinary files only. Read leases are not hostile-namespace or runtime ownership authority.</summary>
    public static ChineseFullClientCatalogSnapshot ReadReferenceDirectory(string referenceRoot, int expectedClientVersion)
    {
        if (expectedClientVersion is <= 0 or > 9999) throw new ArgumentOutOfRangeException(nameof(expectedClientVersion));
        var root = OrdinaryDirectory(referenceRoot);
        var paths = new[] { "version.dat", MainItemsPath, SubItemsPath, EchoPath, EchoConfigurationPath };
        var maximums = new[] { 64, MaximumItemBytes, MaximumItemBytes, MaximumOpaqueBytes, MaximumOpaqueBytes };
        var leases = new FileStream?[paths.Length];
        Exception? primary = null;
        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var path = Path.Combine(root, paths[index].Replace('/', Path.DirectorySeparatorChar));
                OrdinaryFile(path);
                leases[index] = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (leases[index]!.Length is <= 0 || leases[index]!.Length > maximums[index])
                    throw new InvalidDataException("Selected input exceeds its declared bounds.");
            }
            var captured = new byte[paths.Length][];
            for (var index = 0; index < paths.Length; index++)
            {
                var input = leases[index]!;
                captured[index] = new byte[checked((int)input.Length)]; input.ReadExactly(captured[index]);
                if (input.ReadByte() != -1 || input.Length != captured[index].Length)
                    throw new IOException("Selected input changed during capture.");
                OrdinaryFile(Path.Combine(root, paths[index].Replace('/', Path.DirectorySeparatorChar)));
            }
            return ParseCaptured(expectedClientVersion, captured[0], captured[1], captured[2], captured[3], captured[4]);
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            List<Exception>? failures = null;
            foreach (var lease in leases)
            {
                try { lease?.Dispose(); }
                catch (Exception error) { (failures ??= []).Add(error); }
            }
            if (failures is not null)
            {
                if (primary is not null) failures.Insert(0, primary);
                throw new AggregateException("Selected input read leases failed to close.", failures);
            }
        }
    }

    private static string OrdinaryDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("An absolute local ordinary reference directory is required.");
        var root = Path.GetFullPath(path);
        if (root.StartsWith("\\\\", StringComparison.Ordinal) || root.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Network and device reference directories are unsupported.");
        RequireOrdinaryAncestors(new DirectoryInfo(root)); return root;
    }

    private static void OrdinaryFile(string path)
    {
        var file = new FileInfo(path); file.Refresh();
        if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("Selected input must be an existing ordinary file.");
        RequireOrdinaryAncestors(file.Directory!);
    }

    private static void RequireOrdinaryAncestors(DirectoryInfo directory)
    {
        for (DirectoryInfo? current = directory; current is not null; current = current.Parent)
        {
            current.Refresh();
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reference directory and ancestors must be ordinary.");
        }
    }
}
