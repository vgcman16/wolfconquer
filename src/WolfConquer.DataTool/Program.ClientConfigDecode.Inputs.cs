using System.Buffers.Binary;
using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private static FileStream OpenConfigDecodeInput(string path)
    {
        ValidateConfigDecodeInput(path);
        var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        try
        {
            RequireConfigDecodeOrdinaryLease(lease);
            ValidateConfigDecodeInput(path);
            return lease;
        }
        catch (Exception primary)
        {
            Exception? cleanup = null;
            DisposeConfigDecodeLease(lease, ref cleanup);
            throw new ConfigDecodeOperationException(primary, cleanup);
        }
    }

    private static (int Input, int Result) ExecuteConfigDecode(ConfigDecodeOptions options, ref bool published)
    {
        FileStream? input = null, publicData = null;
        Exception? primary = null, cleanup = null;
        (int Input, int Result) sizes = default;
        try
        {
            input = OpenConfigDecodeInput(options.Input);
            publicData = OpenConfigDecodeInput(options.PublicData);
            var encoded = ReadConfigDecodeBytes(input, ConfigDecodeMaximumBytes);
            var publicBytes = ReadConfigDecodeBytes(publicData, options.EncodedWords
                ? ConfigDecodeWordsMaximumBytes : ClientConfigType1Block.MaximumModulusBytes);
            var modulus = options.EncodedWords ? DecodeConfigModulusWords(publicBytes) : publicBytes;
            var result = ClientConfigDefaultFileDecoder.Decode(encoded, modulus, options.MaximumTransformed, options.MaximumResult);
            // Full preparation precedes output; actual input leases remain held.
            ValidateConfigDecodeInput(options.Input);
            ValidateConfigDecodeInput(options.PublicData);
            RequireConfigDecodeOrdinaryLease(input);
            RequireConfigDecodeOrdinaryLease(publicData);
            if (input.Length != encoded.Length || publicData.Length != publicBytes.Length)
                throw new IOException("Input length changed during decoding.");
            PublishConfigDecodeBytes(options.Output, result);
            published = true;
            sizes = (encoded.Length, result.Length);
        }
        catch (Exception failure) { primary = failure; }
        finally
        {
            DisposeConfigDecodeLease(publicData, ref cleanup);
            DisposeConfigDecodeLease(input, ref cleanup);
        }
        if (primary is not null || cleanup is not null) ThrowConfigDecodeOperation(primary, cleanup);
        return sizes;
    }

    private static void DisposeConfigDecodeLease(IDisposable? lease, ref Exception? cleanup)
    {
        try { lease?.Dispose(); }
        catch (Exception error) { cleanup = cleanup is null ? error : new AggregateException(cleanup, error); }
    }

    private static byte[] ReadConfigDecodeBytes(FileStream lease, int maximumBytes)
    {
        if (lease.Length is <= 0 || lease.Length > maximumBytes)
            throw new InvalidDataException("Decoder input is empty or exceeds its finite bound.");
        var bytes = new byte[checked((int)lease.Length)];
        lease.ReadExactly(bytes);
        if (lease.ReadByte() != -1 || lease.Length != bytes.Length)
            throw new IOException("Decoder input length changed during its read.");
        return bytes;
    }

    private static byte[] DecodeConfigModulusWords(byte[] bytes)
    {
        ReadOnlyMemory<byte> json = bytes;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) json = json[3..];
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != ClientConfigWordRecipe.WordCount)
            throw new InvalidDataException("Encoded public modulus must be exactly 64 uint32 JSON numbers.");
        var encodedWords = new uint[ClientConfigWordRecipe.WordCount];
        var index = 0;
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number || element.GetRawText().Any(character => character is < '0' or > '9') ||
                !element.TryGetUInt32(out encodedWords[index++]))
                throw new InvalidDataException("Encoded public words must be decimal uint32 values.");
        }
        var words = ClientConfigWordRecipe.Decode(encodedWords);
        var modulus = new byte[checked(words.Length * sizeof(uint))];
        for (index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32BigEndian(modulus.AsSpan(index * sizeof(uint)), words[index]);
        return modulus;
    }
}
