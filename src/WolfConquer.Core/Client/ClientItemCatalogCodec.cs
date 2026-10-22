namespace WolfConquer.Core.Client;

/// <summary>
/// The fixed packed-catalog transform for the supported client catalog profile. This is not authentication or general-purpose cryptography.
/// It does not validate catalog structure, identify other formats, or authorize installation.
/// </summary>
public static class ClientItemCatalogCodec
{
    public const int BlockSize = 12;

    public static byte[] Encode(ReadOnlySpan<byte> plaintext)
    {
        var encoded = new byte[plaintext.Length];
        var fullLength = plaintext.Length - plaintext.Length % BlockSize;
        for (var offset = 0; offset < fullLength; offset += BlockSize)
        {
            ClientItemCatalogThreeWay.Encrypt(
                plaintext.Slice(offset, BlockSize), encoded.AsSpan(offset, BlockSize));
        }

        TransformTail(plaintext, encoded, fullLength);
        return encoded;
    }

    public static byte[] Decode(ReadOnlySpan<byte> packed)
    {
        var decoded = new byte[packed.Length];
        var fullLength = packed.Length - packed.Length % BlockSize;
        for (var offset = 0; offset < fullLength; offset += BlockSize)
        {
            ClientItemCatalogThreeWay.Decrypt(
                packed.Slice(offset, BlockSize), decoded.AsSpan(offset, BlockSize));
        }

        TransformTail(packed, decoded, fullLength);
        return decoded;
    }

    private static void TransformTail(ReadOnlySpan<byte> input, Span<byte> output, int start)
    {
        for (var index = start; index < input.Length; index++)
        {
            output[index] = (byte)(input[index] ^ 0x54);
        }
    }
}
