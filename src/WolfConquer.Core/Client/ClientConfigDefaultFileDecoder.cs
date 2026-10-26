namespace WolfConquer.Core.Client;

/// <summary>Pure byte decoding with an explicit default profile; no file access, text conversion or native activation.</summary>
public static class ClientConfigDefaultFileDecoder
{
    public static byte[] Decode(ReadOnlySpan<byte> encodedContents, ReadOnlySpan<byte> modulusBigEndian,
        int maximumTransformedBytes, int maximumResultBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumResultBytes);
        var transformed = ClientConfigDefaultContents.DecodeChunks(encodedContents, modulusBigEndian, maximumTransformedBytes);
        if (transformed.Length > sizeof(uint) && transformed[0] == 0x1F && transformed[1] == 0x8B)
            return ClientConfigRequestedGzip.Decode(transformed, maximumResultBytes);
        if (transformed.Length > maximumResultBytes)
            throw new InvalidDataException("Decoded configuration bytes exceed the result bound.");
        return transformed;
    }
}
