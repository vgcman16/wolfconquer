namespace WolfConquer.Core.Client;

/// <summary>Decodes the inspected Chinese catalog byte profile using an explicit caller-owned key.</summary>
/// <remarks>
/// Uses the native eleven-round ThreeWay big-endian word contract and XOR54 tail.
/// Performs no key discovery, file access, text conversion or Lua execution.
/// </remarks>
public static partial class ChineseLuaCatalogTransform
{
    public const int MaximumDecodedBytes = 64 * 1024 * 1024;
    private const int BlockSize = 12;
    private const int Rounds = 11;

    public static byte[] Decode(ReadOnlySpan<byte> encoded, ReadOnlySpan<byte> key, int maximumOutputBytes)
    {
        if (key.Length != BlockSize)
            throw new ArgumentException("The explicit catalog key must contain exactly twelve bytes.", nameof(key));
        if (maximumOutputBytes is < 1 or > MaximumDecodedBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        if (encoded.IsEmpty || encoded.Length > maximumOutputBytes)
            throw new InvalidDataException("Catalog input is empty or exceeds the caller's byte bound.");

        var preparedKey = PrepareKey(key);
        var output = encoded.ToArray();
        var fullLength = output.Length - output.Length % BlockSize;
        for (var offset = 0; offset < fullLength; offset += BlockSize)
            DecodeBlock(output.AsSpan(offset, BlockSize), preparedKey);
        for (var offset = fullLength; offset < output.Length; offset++)
            output[offset] ^= 0x54;
        return output;
    }
}
