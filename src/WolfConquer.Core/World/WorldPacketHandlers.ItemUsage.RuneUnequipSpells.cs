using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static PlayerSpellSnapshot RemoveLegacyUnequippedRuneSpell(
        WorldPacketHandlerContext context,
        PlayerSpellSnapshot spells,
        PlayerItemSnapshotItem rune,
        out ushort? removedSpellId)
    {
        removedSpellId = null;
        if (!TryReadItemType(context.WorldData, rune.ItemId, out var itemType))
            return spells; // Existing missing-metadata compatibility, not native direct-index fault parity.

        var runeSpell = LegacyGameItemPacket.CreateRuneSpellOverride(rune.ItemId, itemType.RuneSpellId);
        if (runeSpell is null || runeSpell.Value.SpellId == rune.ItemId)
            return spells;

        // Rune.GetSkillForRune narrows the raw metadata quotient; Remove is keyed by ID, not level.
        var id = unchecked((ushort)(itemType.RuneSpellId / 100));
        if (!spells.Spells.Any(entry => entry.Id == id))
            return spells;

        removedSpellId = id;
        return new PlayerSpellSnapshot(spells.Spells.Where(entry => entry.Id != id).ToArray());
    }
}
