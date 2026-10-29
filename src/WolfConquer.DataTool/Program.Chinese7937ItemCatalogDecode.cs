using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string Chinese7937CatalogDecodeUsage = "Usage: WolfConquer.DataTool client-item-catalog7937-decode <absolute-input> --expected-sha256 <64-hex-packed-pin> [--output-new <absolute-fresh-decoded-path>]";
    private sealed record Chinese7937DecodeOptions(string Input, byte[] Pin, string? Output);
    private sealed record Chinese7937DecodeResult(int Rows, int PackedBytes, byte[] Decoded, string PackedSha256);

    private static int ClientChinese7937CatalogDecodeCommand(string[] args)
    {
        var published = false;
        try
        {
            var options = ParseChinese7937DecodeOptions(args);
            var result = ExecuteChinese7937Decode(options, ref published);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ClientVersion = 7937, result.Rows, FieldsPerRow = Chinese7937ItemCatalogDocument.FieldCount,
                result.PackedBytes, DecodedBytes = result.Decoded.Length, result.PackedSha256,
                DecodedSha256 = Convert.ToHexString(SHA256.HashData(result.Decoded)),
                ExactInverse = true, OutputPublished = published,
                Boundary = "Decoded data only; not a packed client file, text-role proof, runtime catalog or installation.",
            }));
            return 0;
        }
        catch (ConfigDecodeOperationException error)
        {
            WriteChinese7937DecodeFailure(error.Primary.GetType().Name, published, error.Cleanup?.GetType().Name ?? "None");
            return 1;
        }
        catch (Exception error) when (IsConfigDecodeFailure(error))
        {
            WriteChinese7937DecodeFailure(error.GetType().Name, published, "None");
            return 1;
        }
    }

    private static Chinese7937DecodeOptions ParseChinese7937DecodeOptions(string[] args)
    {
        if (args.Length is not (4 or 6) || !args[2].Equals("--expected-sha256", StringComparison.OrdinalIgnoreCase) ||
            args[3].Length != 64 || !args[3].All(Uri.IsHexDigit) ||
            args.Length == 6 && !args[4].Equals("--output-new", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Explicit7937 input/pin and optional fresh output required.");
        var input = ConfigDecodeFullPath(args[1]);
        var output = args.Length == 6 ? ConfigDecodeFullPath(args[5]) : null;
        ValidateConfigDecodeInput(input);
        if (output is not null) ValidateConfigDecodeOutput(output);
        return new(input, Convert.FromHexString(args[3]), output);
    }

    private static Chinese7937DecodeResult ExecuteChinese7937Decode(Chinese7937DecodeOptions options, ref bool published)
    {
        FileStream? input = null;
        Exception? primary = null, cleanup = null;
        Chinese7937DecodeResult? result = null;
        try
        {
            input = new FileStream(options.Input, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > Chinese7937ItemCatalogDocument.MaximumBytes)
                throw new InvalidDataException("Catalog input must contain1..64MiB.");
            var captured = new byte[checked((int)input.Length)]; input.ReadExactly(captured);
            if (input.ReadByte() != -1 || input.Length != captured.Length) throw new IOException("Catalog source changed.");
            ValidateConfigDecodeInput(options.Input);
            var hash = SHA256.HashData(captured);
            if (!CryptographicOperations.FixedTimeEquals(hash, options.Pin)) throw new InvalidDataException("Packed input pin mismatch.");
            var document = Chinese7937ItemCatalogDocument.ParsePacked(captured, Chinese7937ItemCatalogDocument.ClientVersion);
            var decoded = document.ToArray();
            if (options.Output is not null)
            {
                // Shared decoder publisher flushes an owned temporary and promotes without overwrite.
                // Its compound exception retains primary/cleanup failures; no partial final output is published.
                PublishConfigDecodeBytes(options.Output, decoded);
                published = true;
            }
            result = new(document.Rows.Count, captured.Length, decoded, Convert.ToHexString(hash));
        }
        catch (Exception error) { primary = error; }
        finally { DisposeConfigDecodeLease(input, ref cleanup); }
        if (primary is not null || cleanup is not null) ThrowConfigDecodeOperation(primary, cleanup);
        return result!;
    }

    private static void WriteChinese7937DecodeFailure(string error, bool published, string cleanup)
    {
        try
        {
            Console.Error.WriteLine($"Chinese7937 catalog decode failed: {error} OutputPublished={published} Cleanup={cleanup}.");
            Console.Error.WriteLine(Chinese7937CatalogDecodeUsage);
        }
        catch (IOException) { /* A diagnostic failure cannot undo committed create-only output. */ }
    }
}
