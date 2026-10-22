#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgAttackPacket
{
    // Concrete projection of InteractQuery's full thirteen-field attributed contract only,
    // not generic protobuf. Native unsafe packet copy/cursor behavior is retained.
    private static unsafe void SerializeTypedInteractQueryIntoPacket(LegacyNativePacket stream, LegacyNativeInteractQuery value)
    {
        if (stream is null)
            throw new NullReferenceException();
        using MemoryStream output = new MemoryStream();
        SerializeTypedInteractQuery(output, value);
        byte[] array = output.ToArray();
        fixed (byte* src = array)
            stream.memcpy(stream.Memory + stream.Position, src, array.Length);
        stream.SeekForward(array.Length);
    }

    private static void SerializeTypedInteractQuery(Stream output, LegacyNativeInteractQuery value)
    {
        if (value is null)
            return;
        Field(output, 4, value.UID);
        Field(output, 5, value.OpponentUID);
        Field(output, 7, value.X);
        Field(output, 8, value.Y);
        Field(output, 13, value.AtkType);
        // Required signed int32 uses native two's-complement varint, not zig-zag.
        Field(output, 15, unchecked((ulong)(long)value.Damage));
        if (value.CombatGear != 0) Field(output, 16, value.CombatGear);
        Field(output, 17, unchecked((ulong)(long)value.KilledMonster));
        if (value.SpellLevel != 0) Field(output, 18, value.SpellLevel);
        if (value.SpellID != 0) Field(output, 19, value.SpellID);
        Field(output, 21, value.ResponseDamage);
        Field(output, 25, value.Effect);
        Field(output, 26, value.Unknown);
    }

    private static void Field(Stream output, uint field, ulong value)
    {
        Varint(output, (ulong)field << 3);
        Varint(output, value);
    }

    private static void Varint(Stream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }
}
