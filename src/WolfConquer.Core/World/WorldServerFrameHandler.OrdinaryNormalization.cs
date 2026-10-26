using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyOrdinaryNormalization(CharacterSnapshot Character, PlayerItemSnapshot RuleItems,
    PlayerSpellSnapshot Spells, bool NeedsFreshItemIds);

public sealed partial class WorldServerFrameHandler
{
    internal LegacyOrdinaryNormalization PrepareOrdinaryNormalization(CharacterSnapshot character,
        PlayerItemSnapshot savedItems, PlayerSpellSnapshot savedSpells, CombatGearSnapshot gear)
    {
        // Existing saved state is not birth/migration authority. Source ordinary
        // MsgLoginHandler392–394 adds only a missing Trojan-lineage Cyclone here.
        // True level progression and new-role work retain their separate owners.
        _ = gear;
        var spells = savedSpells;
        foreach (var id in LegacyClassOwnedSpellGrantProfile.GetSpellIds(character))
            if (!spells.ContainsSpell(id)) spells = LegacySkillBookUse.AddOrUpdateSpell(spells, CreateLegacyClassOwnedSpellEntry(id));
        return new(character, savedItems, spells, NeedsFreshItemIds: false);
    }
}
