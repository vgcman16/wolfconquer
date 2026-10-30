namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    public static LegacyNativeMsgSpell? GetSkillForRune(LegacyNativeGameItemRuntime item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.SpellID == item.ITEM_ID)
        {
            return null;
        }
        var items = LegacyNativePoolItems.ItemsBase;
        if (items is null || !items.TryGetValue(item.ITEM_ID, out var metadata) || metadata is null)
            throw new InvalidOperationException("runeSpellMetadataMissing");
        var encoded = metadata.RuneSpellID;
        // SpellID is a display field: raw-self levels above01 display their
        // base01 identity with Effect1. They still grant no learned ability.
        if (encoded == 0 || LegacyRuneSpellIdentity.IsRawSelf(item.ITEM_ID, encoded)) return null;
        var quotient = encoded / 100;
        if (quotient is 0 or > ushort.MaxValue)
            throw new InvalidOperationException("runeSpellMetadataIdOutsideUInt16");
        return new LegacyNativeMsgSpell
        {
            ID = checked((ushort)quotient),
            Level = (byte)(encoded % 100)
        };
    }

    public static uint GetRuneFromSkill(LegacyNativeMsgSpell skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (skill.ID == 0 || skill.Level > 99)
            throw new ArgumentOutOfRangeException(nameof(skill), "Rune identity requires a nonzero ID and two decimal level digits.");
        var items = LegacyNativePoolItems.ItemsBase ?? throw new InvalidOperationException("runeMetadataOwnerMissing");
        var encoded = checked((uint)skill.ID * 100 + skill.Level);
        LegacyNativeItemRuneMetadata? first = null;
        foreach (var row in items.Values)
        {
            if (row is null) throw new InvalidOperationException("runeMetadataRowMissing");
            if (first is null && row.RuneSpellID == encoded &&
                !LegacyRuneSpellIdentity.IsRawSelf(row.ID, row.RuneSpellID)) first = row;
        }
        return first?.ID ?? 0u;
    }
}
