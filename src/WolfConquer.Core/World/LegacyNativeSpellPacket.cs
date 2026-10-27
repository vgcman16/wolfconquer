using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Source-shaped GetSpell and managed preparation before bounded SpellCreate storage.
internal static partial class LegacyNativeSpellPacket
{
    public static void GetSpell(this LegacyNativePacket stream, out LegacyNativeMsgSpell spell)
    {
        spell = new LegacyNativeMsgSpell();
        spell.Experience = stream.ReadInt32();
        spell.ID = stream.ReadUInt16();
        spell.UnKnow = stream.ReadUInt16();
        spell.Level = stream.ReadUInt16();
        spell.Type = stream.ReadUInt16();
        spell.UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)stream.ReadUInt32();
        spell.Soul = stream.ReadUInt8();
        spell.HashLevelSoul = stream.ReadInt32();
    }

    public static LegacyNativePacket SpellCreate(this LegacyNativePacket stream, LegacyNativeMsgSpell spell)
    {
        LegacyNativePreparedPacketStorage.Validate(stream, 4);
        var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
        var frame = PrepareSpellFrame(spell, seal);
        LegacyNativePreparedPacketStorage.Write(stream, frame);
        return stream;
    }
}
