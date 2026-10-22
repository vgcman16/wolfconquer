using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal class LegacyNativeClientSpells
{
    // Native implicit sequential packing, not Pack=1: ID0, Level2, Experience4,
    // PreviousLevel8, SoulLevel9, UseJiangSpell10, tail padding11; expected size12.
    public struct DBSpell
    {
        public ushort ID;
        public ushort Level;
        public int Experience;
        public byte PreviousLevel;
        public byte SoulLevel;
        public byte UseJiangSpell;

        public DBSpell GetDBSpell(LegacyNativeMsgSpell spell)
        {
            ID = spell.ID;
            Level = spell.Level;
            Experience = spell.Experience;
            PreviousLevel = spell.PreviousLevel;
            SoulLevel = spell.SoulLevel;
            UseJiangSpell = spell.UseSpellSoul;
            return this;
        }

        public LegacyNativeMsgSpell GetClientSpell()
        {
            LegacyNativeMsgSpell msgSpell = new LegacyNativeMsgSpell();
            msgSpell.ID = ID;
            msgSpell.Level = Level;
            msgSpell.Experience = Experience;
            msgSpell.PreviousLevel = PreviousLevel;
            msgSpell.SoulLevel = SoulLevel;
            msgSpell.UseSpellSoul = UseJiangSpell;
            return msgSpell;
        }
    }
}
