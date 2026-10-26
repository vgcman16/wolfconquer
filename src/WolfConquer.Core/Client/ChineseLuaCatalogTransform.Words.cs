using System.Numerics;

namespace WolfConquer.Core.Client;

public static partial class ChineseLuaCatalogTransform
{
    // Algebra corroborated against Crypto++ 8.9's public-domain 3way.cpp;
    // the inspected native block byte order is handled separately above.
    private static void Theta(ref uint a, ref uint b, ref uint c)
    {
        var t = a ^ b ^ c;
        t = BitOperations.RotateLeft(t, 16) ^ BitOperations.RotateLeft(t, 8);
        var b0 = (a << 24) ^ (c >> 8) ^ (b << 8) ^ (a >> 24);
        var b1 = (b << 24) ^ (a >> 8) ^ (c << 8) ^ (b >> 24);
        a ^= t ^ b0;
        b ^= t ^ b1;
        c ^= t ^ (b0 >> 16) ^ (b1 << 16);
    }

    private static void Mu(ref uint a, ref uint b, ref uint c)
    {
        b = ReverseByteBits(b);
        var first = ReverseByteBits(a);
        a = ReverseByteBits(c);
        c = first;
    }

    private static uint ReverseByteBits(uint value)
    {
        value = ((value & 0xAAAAAAAA) >> 1) | ((value & 0x55555555) << 1);
        value = ((value & 0xCCCCCCCC) >> 2) | ((value & 0x33333333) << 2);
        return ((value & 0xF0F0F0F0) >> 4) | ((value & 0x0F0F0F0F) << 4);
    }

    private static void PiGammaPi(ref uint a, ref uint b, ref uint c)
    {
        var b2 = BitOperations.RotateLeft(c, 1);
        var b0 = BitOperations.RotateLeft(a, 22);
        a = BitOperations.RotateLeft(b0 ^ (b | ~b2), 1);
        c = BitOperations.RotateLeft(b2 ^ (b0 | ~b), 22);
        b ^= b2 | ~b0;
    }
}
