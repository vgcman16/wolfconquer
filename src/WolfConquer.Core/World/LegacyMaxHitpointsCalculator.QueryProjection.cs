using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyEquipmentQueryHitpointProjection(
    uint OtherBeforeVip, uint SubClass, uint RuneStorage);

public static partial class LegacyMaxHitpointsCalculator
{
    internal static uint CalculateQueryCircleEnchantHitpoints(PlayerItemSnapshotItem item, byte level)
    {
        // Snapshot Enchant is already byte (source clamps to255); source clears
        // these special slots BEFORE Circle's switch, including its +=0 setter.
        var enchant = item.Position % 50 is 0 or 7 or 10 or 11 or 12 or 13 or 15 or 16 or 17 or 18 or 19
            ? 0U : item.Enchant;
        return level switch
        {
            1 or 2 => enchant / 2,
            3 => (uint)(enchant / 1.5),
            4 => enchant,
            5 => (uint)(enchant * 1.2),
            6 => (uint)(enchant * 1.4),
            7 => (uint)(enchant * 1.6),
            8 => (uint)(enchant * 1.8),
            9 => enchant * 2,
            _ => 0,
        };
    }

    internal static ushort CalculateQueryMaxVigor(
        IReadOnlyList<PlayerItemSnapshotItem> equipment, WorldDataCatalog? catalog)
    {
        ushort maximum = 0;
        foreach (var item in equipment)
        {
            if (item.Durability == 0) continue;
            var position = item.Position % 50;
            if (position == 18) maximum = unchecked((ushort)(maximum + 1_000));
            var plus = Math.Min((int)item.Plus, 12);
            if (position == 12 && plus > 0 &&
                TryGetItemAddBonus(catalog?.ItemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var bonus) &&
                bonus.Values.Count > 6)
                maximum = unchecked((ushort)bonus.Values[6]);
        }

        return maximum;
    }

    internal static uint CalculateQueryItemHitpoints(
        PlayerItemSnapshotItem item, WorldDataCatalog? catalog, bool alternate)
    {
        var single = PlayerItemSnapshot.Empty with { EquipmentItems = [item] };
        var equipment = CalculateEquipmentHitpoints(single, catalog?.ItemsById,
            catalog?.ItemAddBonusByItemTypeIdAndLevel, alternate);
        var secondary = LegacyItemSecondaryHitpointsCalculator.Calculate(
            single, catalog, alternateEquipmentActive: alternate);
        return unchecked(equipment + secondary.RelicAttributeHitpoints + secondary.RefineryHitpoints);
    }

    internal static LegacyEquipmentQueryHitpointProjection CalculateQueryHitpointProjection(
        WorldSession session, PlayerItemSnapshot sourceItems, WorldDataCatalog? catalog, bool alternate)
    {
        // Calculate() has no runtime HP floor or transform override. Split its
        // already-modeled components; NEVER use CalculateRuntimeMaxHitpoints here.
        var modeled = Calculate(session.Character, sourceItems,
            session.CombatGear ?? CombatGearSnapshot.Empty, catalog,
            alternateEquipmentActive: alternate);
        var circle = CalculateCircleOfLifeEnchantHitpoints(
            session.Character, sourceItems, catalog?.ItemsById, alternate);
        var rune = LegacyRuneHitpointsCalculator.Calculate(sourceItems, catalog).Total;
        var subclass = LegacySubClassHitpointsCalculator.Calculate(session.Character, catalog).Total;
        var itemTotal = 0U;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(sourceItems, alternate))
        {
            if (item.Durability != 0)
                itemTotal = unchecked(itemTotal + CalculateQueryItemHitpoints(item, catalog, alternate));
        }

        // Preserve the existing raw stored fallback for not-yet-modeled inputs;
        // neither cached Status nor current HP is an accumulating floor.
        var raw = Math.Min(uint.MaxValue, Math.Max((ulong)modeled.Total, ReadStoredMaxHitpoints(session.Character)));
        // Native Equip1554 reads base attributes AFTER Gear.Status and Mythsoul publications.
        // Leave all other already-modeled pre-Gear components in this captured projection.
        var split = (ulong)modeled.AttributeHitpoints + circle + rune + subclass + itemTotal;
        return new((uint)(raw > split ? raw - split : 0), subclass, rune);
    }
}
