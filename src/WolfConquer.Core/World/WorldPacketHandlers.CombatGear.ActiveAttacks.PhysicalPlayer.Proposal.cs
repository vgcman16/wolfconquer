using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Historical RNG result only; the Frame's private registration is authority.
    internal sealed record LegacyPhysicalCombatGearSelection(uint GearItemId, ushort SpellId,
        bool PiratePassive, PlayerSpellSnapshotEntry? PlayerSpell, LegacyMagicRuntimeSpell? Spell);

    internal static bool HasOwnedDragonhowlCandidate(WorldSession actor) =>
        HasLegacyCombatGearEquippedForClass(actor.CombatGear ?? CombatGearSnapshot.Empty,
            actor.Character.Class, LegacyCombatGearDragonhowlItemId);

    internal static LegacyPhysicalCombatGearSelection? SelectOwnedDragonhowl(
        WorldSession actor, WorldSession target, WorldDataCatalog catalog,
        LegacyOwnedPhysicalMetadataWitness metadata)
        => SelectOwnedDragonhowl(actor, target.Character.X, target.Character.Y, catalog, metadata);

    internal static LegacyPhysicalCombatGearSelection? SelectOwnedDragonhowl(
        WorldSession actor, long targetX, long targetY, WorldDataCatalog catalog,
        LegacyOwnedPhysicalMetadataWitness metadata)
    {
        if (!HasOwnedDragonhowlCandidate(actor)) return null;
        // These are exactly the two learned-rate rows the Dragonhowl selector
        // reads before its fallback draw. Unrelated learned rows are not pinned.
        foreach (ushort id in new ushort[] { 17_490, 17_650 })
        {
            var learned = actor.PlayerSpells.Spells.FirstOrDefault(row => row.Id == id);
            if (learned is not null) metadata.UseMagicLevel(id, learned.Level, required: true);
        }
        if (!TrySelectLegacyDirectPhysicalCombatGearSpell(actor, catalog, targetX, targetY,
                out var gear, out var spellId, out var pirate)) return null;
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(actor.PlayerSpells, catalog, spellId,
                out var playerSpell, out var spell)) return new(gear, spellId, pirate, null, null);
        metadata.RequireRuntimeSpell(spell);
        return new(gear, spellId, pirate, playerSpell, spell);
    }
}
