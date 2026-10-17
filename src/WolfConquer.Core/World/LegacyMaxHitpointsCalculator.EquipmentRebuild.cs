using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMaxHitpointsCalculator
{
    public static uint CalculateEquipmentRebuildMaxHitpoints(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? catalog,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(combatGear);

        // Preserve the existing transform policy; broader transformed Status ownership is separate.
        if (LegacyTransformState.TryGetActiveMaxHitpoints(snapshot, out var transformed))
        {
            return transformed;
        }

        var computed = Calculate(snapshot, playerItems, combatGear, catalog,
            alternateEquipmentActive: alternateEquipmentActive).Total;
        // Stored raw-max fallback is retained for inputs not yet modeled. Neither
        // current HP nor the admitted session cache is an accumulating raw floor.
        var raw = Math.Max((ulong)computed, ReadStoredMaxHitpoints(snapshot));
        var maximum = raw > uint.MaxValue ? uint.MaxValue : (uint)raw;
        if (HasLegacyStatusFlag(snapshot.Attributes, 212) &&
            TryReadUIntAttribute(snapshot, "FineRainPower", out var power))
        {
            maximum = unchecked(maximum + power); // Equip.cs1809-1812 uint addition, exactly once.
        }

        return maximum;
    }
}
