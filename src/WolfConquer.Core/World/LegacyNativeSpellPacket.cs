using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Full native SpellPacket primitive GetSpell/SpellCreate contract.
internal static class LegacyNativeSpellPacket
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
        stream.InitWriter();
        stream.Write(spell.Experience);
        stream.Write(spell.ID);
        stream.Write(spell.UnKnow);
        stream.Write(spell.Level);
        stream.Write(spell.Type);
        stream.Write((uint)spell.UseSoul);
        stream.Write(spell.Soul);
        stream.Write(spell.HashLevelSoul);
        stream.Finalize(2101);
        return stream;
    }
}
