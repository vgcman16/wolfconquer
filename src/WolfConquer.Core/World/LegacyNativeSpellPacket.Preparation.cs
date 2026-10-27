using System.Buffers.Binary;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeSpellPacket
{
    internal static byte[] PrepareSpellFrame(LegacyNativeMsgSpell spell, string? seal)
    {
        ArgumentNullException.ThrowIfNull(spell);
        var payload = new byte[21];
        BinaryPrimitives.WriteInt32LittleEndian(payload, spell.Experience);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), spell.ID);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), spell.UnKnow);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), spell.Level);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), spell.Type);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), (uint)spell.UseSoul);
        payload[16] = spell.Soul;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(17), spell.HashLevelSoul);
        return LegacyNativePreparedPacketFrames.Frame(2101, payload, seal);
    }
}
