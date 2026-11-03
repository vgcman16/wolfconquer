using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

// Caller-owned parsed DATA, not a session, current revision or publication receipt.
internal sealed class SqlOrdinaryAdmissionData
{
    internal SqlOrdinaryAdmissionData(SqlWorldStateBundle source, CharacterSnapshot character,
        PlayerItemSnapshot items, PlayerSpellSnapshot spells, PlayerProficiencySnapshot proficiencies,
        CombatGearSnapshot combatGear)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(proficiencies);
        ArgumentNullException.ThrowIfNull(combatGear);
        Source = source; Character = character; Items = items; Spells = spells;
        Proficiencies = proficiencies; CombatGear = combatGear;
    }

    internal SqlWorldStateBundle Source { get; }
    internal CharacterSnapshot Character { get; }
    internal PlayerItemSnapshot Items { get; }
    internal PlayerSpellSnapshot Spells { get; }
    internal PlayerProficiencySnapshot Proficiencies { get; }
    internal CombatGearSnapshot CombatGear { get; }
}
