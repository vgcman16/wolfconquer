namespace WolfConquer.Core.Client;

/// <summary>
/// Decodes fixed-width chunks under the caller-selected default profile. This
/// performs no file access, gzip expansion, text parsing or provider discovery.
/// </summary>
public static class ClientConfigDefaultContents
{
    // An offline tooling memory bound, not a native file-format length limit.
    public const int MaximumEncodedBytes = 64 * 1024 * 1024;

    public static byte[] DecodeChunks(ReadOnlySpan<byte> encodedContents,
        ReadOnlySpan<byte> modulusBigEndian, int maximumOutputBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumOutputBytes, 1);
        if (encodedContents.IsEmpty || encodedContents.Length > MaximumEncodedBytes)
        {
            throw new InvalidDataException("Encoded contents are empty or exceed the offline tooling bound.");
        }

        while (!modulusBigEndian.IsEmpty && modulusBigEndian[0] == 0)
        {
            modulusBigEndian = modulusBigEndian[1..];
        }

        if (modulusBigEndian.Length is < 11 or > ClientConfigType1Block.MaximumModulusBytes)
        {
            throw new ArgumentException("The modulus width is outside the explicit default profile.", nameof(modulusBigEndian));
        }

        var encoded = encodedContents.ToArray();
        var modulus = modulusBigEndian.ToArray();
        var width = modulus.Length;
        var rawAllocationBound = checked(encoded.Length + 1);
        using var output = new MemoryStream();
        var written = 0;
        for (var offset = 0; offset < encoded.Length;)
        {
            // The ordinary read requests a modulus-sized chunk and may return
            // fewer bytes only for the final read. Never infer/pad boundaries.
            var count = Math.Min(width, encoded.Length - offset);
            var payload = ClientConfigDefaultPublicTransform.DecodeBlock(encoded.AsSpan(offset, count), modulus, width);
            if (payload.Length == 0)
            {
                throw new InvalidDataException("The outer configuration loader requires positive output from every chunk.");
            }

            if (payload.Length > rawAllocationBound - written || payload.Length > maximumOutputBytes - written)
            {
                throw new InvalidDataException("Accumulated configuration output exceeds the available bound.");
            }

            output.Write(payload);
            written = checked(written + payload.Length);
            offset += count;
        }

        // No payload is published until every chunk and cumulative bound passes.
        return output.ToArray();
    }
}
