using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgAttackPacket
{
    // Native87..148 construction and bit transforms. Interaction read, generic deserialization,
    // HandlerProcess/Process and ProcescMagic remain absent, not no-op dispatchers.
    public static LegacyNativePacket InteractionCreate(this LegacyNativePacket stream, LegacyNativeInteractQuery pQuery)
    {
        stream.InitWriter();
        SerializeTypedInteractQueryIntoPacket(stream, pQuery);
        stream.Finalize(2135);
        return stream;
    }

    public static void EncodeMagicAttack(LegacyNativeInteractQuery pQuery)
    {
        BitUnfold32(pQuery.Damage, out var lower, out var upper);
        lower = (ushort)(ExchangeShortBits((uint)(lower - 5310), 3) ^ pQuery.UID ^ 0x915D);
        upper ^= 0x3721;
        pQuery.Damage = BitFold32(lower, upper);
        pQuery.OpponentUID = ExchangeLongBits((uint)((int)pQuery.OpponentUID - -1953450726) ^ pQuery.UID ^ 0x5F2D2463, 19);
        pQuery.X = (ushort)(ExchangeShortBits((uint)(pQuery.X - 56594), 1) ^ pQuery.UID ^ 0x2ED6);
        pQuery.Y = (ushort)(ExchangeShortBits((uint)(pQuery.Y - 30430), 5) ^ pQuery.UID ^ 0xB99B);
    }

    private static void DecodeMagicAttack(LegacyNativeInteractQuery pQuery, LegacyNativeGameClientRuntime user)
    {
        BitUnfold32(pQuery.SpellID, out var lower, out var upper);
        lower = (ushort)(ExchangeShortBits((ushort)lower ^ pQuery.UID ^ 0x915D, 13) + 5310);
        upper = (ushort)((byte)upper ^ 0x21);
        pQuery.SpellID = (ushort)BitFold32(lower, upper);
        pQuery.OpponentUID = (ExchangeLongBits(pQuery.OpponentUID, 13) ^ pQuery.UID ^ 0x5F2D2463) + 2341516570u;
        pQuery.X = (ushort)(ExchangeShortBits(pQuery.X ^ pQuery.UID ^ 0x2ED6, 15) + 56594);
        pQuery.Y = (ushort)(ExchangeShortBits(pQuery.Y ^ pQuery.UID ^ 0xB99B, 11) + 30430);
    }

    public static int BitFold32(int lower16, int higher16)
    {
        return lower16 | (higher16 << 16);
    }

    public static void BitUnfold32(int bits32, out int lower16, out int upper16)
    {
        lower16 = bits32 & 0xFFFF;
        upper16 = bits32 >> 16;
    }

    public static void BitUnfold64(ulong bits64, out int lower32, out int upper32)
    {
        lower32 = (int)(bits64 & 0xFFFFFFFFu);
        upper32 = (int)(bits64 >> 32);
    }

    private static uint ExchangeShortBits(uint data, int bits)
    {
        data &= 0xFFFF;
        return ((data >> bits) | (data << 16 - bits)) & 0xFFFF;
    }

    public static uint ExchangeLongBits(uint data, int bits)
    {
        return (data >> bits) | (data << 32 - bits);
    }
}
