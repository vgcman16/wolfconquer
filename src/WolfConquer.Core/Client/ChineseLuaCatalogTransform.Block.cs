using System.Buffers.Binary;

namespace WolfConquer.Core.Client;

public static partial class ChineseLuaCatalogTransform
{
    private static (uint A, uint B, uint C) PrepareKey(ReadOnlySpan<byte> key)
    {
        var a = BinaryPrimitives.ReadUInt32BigEndian(key);
        var b = BinaryPrimitives.ReadUInt32BigEndian(key[4..]);
        var c = BinaryPrimitives.ReadUInt32BigEndian(key[8..]);
        Theta(ref a, ref b, ref c);
        Mu(ref a, ref b, ref c);
        return (BinaryPrimitives.ReverseEndianness(a), BinaryPrimitives.ReverseEndianness(b),
            BinaryPrimitives.ReverseEndianness(c));
    }

    private static void DecodeBlock(Span<byte> block, (uint A, uint B, uint C) key)
    {
        var a = BinaryPrimitives.ReadUInt32BigEndian(block);
        var b = BinaryPrimitives.ReadUInt32BigEndian(block[4..]);
        var c = BinaryPrimitives.ReadUInt32BigEndian(block[8..]);
        Mu(ref a, ref b, ref c);
        uint roundConstant = 0xB1B1;
        for (var round = 0; round < Rounds; round++)
        {
            a ^= key.A ^ (roundConstant << 16);
            b ^= key.B;
            c ^= key.C ^ roundConstant;
            Theta(ref a, ref b, ref c);
            PiGammaPi(ref a, ref b, ref c);
            roundConstant <<= 1;
            if ((roundConstant & 0x10000) != 0) roundConstant ^= 0x11011;
        }
        a ^= key.A ^ (roundConstant << 16);
        b ^= key.B;
        c ^= key.C ^ roundConstant;
        Theta(ref a, ref b, ref c);
        Mu(ref a, ref b, ref c);
        BinaryPrimitives.WriteUInt32BigEndian(block, a);
        BinaryPrimitives.WriteUInt32BigEndian(block[4..], b);
        BinaryPrimitives.WriteUInt32BigEndian(block[8..], c);
    }
}
