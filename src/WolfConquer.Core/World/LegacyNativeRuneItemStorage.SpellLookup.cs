#nullable disable

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    public static LegacyNativeMsgSpell GetSkillForRune(LegacyNativeGameItemRuntime item)
    {
        if (item.SpellID == item.ITEM_ID)
        {
            return null;
        }
        return new LegacyNativeMsgSpell
        {
            ID = (ushort)(LegacyNativePoolItems.ItemsBase[item.ITEM_ID].RuneSpellID / 100),
            Level = (byte)(LegacyNativePoolItems.ItemsBase[item.ITEM_ID].RuneSpellID % 100)
        };
    }

    public static uint GetRuneFromSkill(LegacyNativeMsgSpell skill)
    {
        return (LegacyNativePoolItems.ItemsBase.Values.Where((LegacyNativeItemRuneMetadata i) =>
            i.RuneSpellID == skill.ID * 100 + skill.Level).FirstOrDefault() != null)
            ? LegacyNativePoolItems.ItemsBase.Values.Where((LegacyNativeItemRuneMetadata i) =>
                i.RuneSpellID == skill.ID * 100 + skill.Level).FirstOrDefault().ID
            : 0u;
    }
}
