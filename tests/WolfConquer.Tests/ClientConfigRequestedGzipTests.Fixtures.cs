using System.Buffers.Binary;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigRequestedGzipTests
{
    private static byte[] Request(byte[] member, uint count)
    {
        var result = new byte[member.Length + 5];
        member.CopyTo(result, 0);
        result[member.Length] = 0xcc; // Explicit non-gzip suffix before the outer request word.
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(member.Length + 1), count);
        return result;
    }

    private static byte[] Wrap(byte[] raw, uint checksum, uint size)
    {
        byte[] header = [0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 0, 3];
        var result = new byte[header.Length + raw.Length + 8];
        header.CopyTo(result, 0);
        raw.CopyTo(result, header.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - 8), checksum);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - 4), size);
        return result;
    }

    private static uint KnownACrc(int count) => count switch
    {
        16384 => 0x964AC3AC,
        16385 => 0x0C2F3B8B,
        _ => throw new ArgumentOutOfRangeException(nameof(count))
    };

    private static byte[] StoredA(int count)
    {
        var raw = new byte[count + 5];
        raw[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(1), (ushort)count);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(3), (ushort)(count ^ 0xffff));
        raw.AsSpan(5).Fill(65);
        return Wrap(raw, KnownACrc(count), (uint)count);
    }

    private static byte[] LiteralA(int count)
    {
        // Only two independently known fixed words are used; no canonical
        // codebook builder or production inflater participates in this fixture.
        var bits = new ClientConfigDeflateTestBits().Low(1, 1).Low(1, 2);
        for (var index = 0; index < count; index++) bits.Code("01110001");
        bits.Code("0000000");
        return Wrap(bits.ToArray(), KnownACrc(count), (uint)count);
    }
}
