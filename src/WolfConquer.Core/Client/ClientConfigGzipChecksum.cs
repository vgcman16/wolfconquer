namespace WolfConquer.Core.Client;

internal static class ClientConfigGzipChecksum
{
    internal const uint Initial = uint.MaxValue;

    internal static uint Add(uint current, byte value)
    {
        current ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            current = current >> 1 ^ ((current & 1) == 0 ? 0 : 0xEDB88320u);
        }

        return current;
    }

    internal static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var checksum = Initial;
        foreach (var value in bytes) checksum = Add(checksum, value);
        return checksum ^ uint.MaxValue;
    }
}
