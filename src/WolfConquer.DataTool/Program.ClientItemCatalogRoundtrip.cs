using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string ClientItemCatalogRoundtripUsage =
        "Usage: WolfConquer.DataTool client-item-catalog-roundtrip <decoded-source-path> [--output-new <fresh-path>]";
    private const int ClientItemCatalogMaximumBytes = 64 * 1024 * 1024;

    private static int ClientItemCatalogRoundtrip(string[] args)
    {
        try
        {
            if ((args.Length != 2 && args.Length != 4) ||
                string.IsNullOrWhiteSpace(args[1]) || args[1].StartsWith("--", StringComparison.Ordinal) ||
                (args.Length == 4 &&
                    (!string.Equals(args[2], "--output-new", StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrWhiteSpace(args[3]) || args[3].StartsWith("--", StringComparison.Ordinal))))
            {
                throw new ArgumentException("Expected a decoded source path and, optionally, one --output-new path.");
            }

            var sourcePath = Path.GetFullPath(args[1]);
            var outputPath = args.Length == 4 ? Path.GetFullPath(args[3]) : null;
            ValidateClientItemCatalogPath(sourcePath, mustBeNew: false);
            if (outputPath is not null)
            {
                ValidateClientItemCatalogPath(outputPath, mustBeNew: true);
            }

            byte[] source;
            using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > ClientItemCatalogMaximumBytes)
                {
                    throw new InvalidDataException("Decoded catalog input exceeds the 64 MiB limit.");
                }

                source = new byte[checked((int)input.Length)];
                input.ReadExactly(source);
                if (input.ReadByte() != -1)
                {
                    throw new IOException("Decoded catalog input changed while reading.");
                }
            }

            var document = ClientItemCatalogDocument.Parse(source);
            var reconstructed = document.ToArray();
            if (!source.AsSpan().SequenceEqual(reconstructed))
            {
                throw new InvalidDataException("Decoded catalog reconstruction was not byte-exact; no output was written.");
            }

            if (outputPath is not null)
            {
                // Recheck immediately before CreateNew. Existing files are never overwritten.
                ValidateClientItemCatalogPath(outputPath, mustBeNew: true);
                using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(reconstructed);
            }

            Console.WriteLine($"Rows={document.Rows.Count} FieldsPerRow={ClientItemCatalogDocument.FieldCount} Bytes={source.Length} ExactRoundtrip=True");
            Console.WriteLine("Decoded catalog only; this is not a packed client file and must not be installed as itemtype.dat.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            Console.Error.WriteLine($"Client item catalog roundtrip failed: {ex.Message}");
            Console.Error.WriteLine(ClientItemCatalogRoundtripUsage);
            return 1;
        }
    }

    private static void ValidateClientItemCatalogPath(string path, bool mustBeNew)
    {
        FileAttributes? leafAttributes = null;
        try
        {
            leafAttributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException) when (mustBeNew)
        {
            // Only a missing output leaf is allowed; all ancestors must exist.
        }

        if (leafAttributes is { } attributes)
        {
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Catalog paths must not be reparse points.");
            }

            if (mustBeNew || (attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException(mustBeNew ? "Output path already exists." : "Decoded source must be a regular file.");
            }
        }

        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
        {
            var parentAttributes = File.GetAttributes(parent.FullName);
            if ((parentAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Catalog path ancestors must not be reparse points.");
            }

            if ((parentAttributes & FileAttributes.Directory) == 0)
            {
                throw new IOException("Catalog path ancestors must be existing directories.");
            }
        }
    }
}
