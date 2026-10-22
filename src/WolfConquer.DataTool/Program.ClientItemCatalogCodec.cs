using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string ClientItemCatalogCodecUsage =
        "Usage: WolfConquer.DataTool client-item-catalog-codec <decode|encode> <input-path> [--output-new <fresh-path>]";

    private static int ClientItemCatalogCodecCommand(string[] args)
    {
        try
        {
            if ((args.Length != 3 && args.Length != 5) ||
                (!string.Equals(args[1], "decode", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(args[1], "encode", StringComparison.OrdinalIgnoreCase)) ||
                string.IsNullOrWhiteSpace(args[2]) || args[2].StartsWith("--", StringComparison.Ordinal) ||
                (args.Length == 5 &&
                    (!string.Equals(args[3], "--output-new", StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrWhiteSpace(args[4]) || args[4].StartsWith("--", StringComparison.Ordinal))))
            {
                throw new ArgumentException("Expected decode or encode, one input path, and optionally one --output-new path.");
            }

            var mode = args[1].ToLowerInvariant();
            var sourcePath = Path.GetFullPath(args[2]);
            var outputPath = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
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
                    throw new InvalidDataException("Catalog input exceeds the 64 MiB limit.");
                }

                source = new byte[checked((int)input.Length)];
                input.ReadExactly(source);
                if (input.ReadByte() != -1)
                {
                    throw new IOException("Catalog input changed while reading.");
                }
            }

            var decoded = mode == "decode" ? ClientItemCatalogCodec.Decode(source) : source;
            var document = ClientItemCatalogDocument.Parse(decoded);
            if (!decoded.AsSpan().SequenceEqual(document.ToArray()))
            {
                throw new InvalidDataException("Decoded document reconstruction was not byte-exact; no output was written.");
            }

            var encoded = ClientItemCatalogCodec.Encode(decoded);
            var outputBytes = mode == "decode" ? decoded : encoded;
            var verified = mode == "decode" ? encoded : ClientItemCatalogCodec.Decode(encoded);
            if (outputBytes.Length != source.Length || !source.AsSpan().SequenceEqual(verified))
            {
                throw new InvalidDataException("Catalog codec roundtrip was not byte-exact; no output was written.");
            }

            if (outputPath is not null)
            {
                // All schema/byte checks precede publication. Recheck the path,
                // then let CreateNew reject an existing or racing output leaf.
                ValidateClientItemCatalogPath(outputPath, mustBeNew: true);
                using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(outputBytes);
            }

            Console.WriteLine($"Mode={mode} Rows={document.Rows.Count} FieldsPerRow={ClientItemCatalogDocument.FieldCount} InputBytes={source.Length} OutputBytes={outputBytes.Length} ExactRoundtrip=True");
            Console.WriteLine(mode == "decode"
                ? "Decoded catalog only; this is not a packed client file and must not be installed as itemtype.dat or ItemtypeSub.dat."
                : "Encoded catalog candidate only; this does not prove live-client loading or rendering.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            Console.Error.WriteLine($"Client item catalog codec failed: {ex.Message}");
            Console.Error.WriteLine(ClientItemCatalogCodecUsage);
            return 1;
        }
    }
}
