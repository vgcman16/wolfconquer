using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyEquipmentQueryHitpointReplay PreflightAttributeHitpoints(WorldSession source,
        IReadOnlyList<PlayerItemSnapshotItem> equipment, WorldDataCatalog? data, LegacyOwnedPhysicalMetadataWitness metadata)
    {
        var hp = new LegacyEquipmentQueryHitpointReplay(source.Character.MinHitPoints);
        _ = LegacyEquipmentQueryBounds.WithHitpoints(source.Character, hp.Current);
        foreach (var item in equipment)
        {
            if (item.Durability == 0) continue;
            if (data is not null && TryGetLegacyEligibleEquippedRuneLevel(source, data, "Circle of Life", out var circle) && circle is >= 1 and <= 9)
            {
                uint addition = LegacyMaxHitpointsCalculator.CalculateQueryCircleEnchantHitpoints(item, circle);
                hp.AddMaximum(addition); hp.AddCurrent(addition);
            }
            hp.AddMaximum(LegacyMaxHitpointsCalculator.CalculateQueryItemHitpoints(item, data, source.AlternateEquipmentActive));
        }
        if (data is not null && HasLegacyStatusFlag(source.Character, LegacyIronGuardFlag) &&
            TryGetLegacyEligibleEquippedRuneLevel(source, data, "FuryStrike", out var fury))
        {
            uint addition = EquipmentQueryFuryMaximum(fury);
            hp.AddCurrent(addition); hp.AddMaximum(addition);
        }
        var sourceItems = source.PlayerItems with { EquipmentItems = equipment };
        var projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(source, sourceItems, data, source.AlternateEquipmentActive);
        hp.AddMaximum(projection.OtherBeforeVip);
        hp.AddMaximum(LegacyMaxHitpointsCalculator.CalculateAttributeHitpointsChecked(source.Character));
        if (ReadAttributeUInt32(source.Character.Attributes, "VipLevel") == 7)
        { hp.AddCurrent(3_000); hp.AddMaximum(3_000); }
        hp.AddMaximum(projection.SubClass);
        hp.AddMaximum(projection.RuneStorage);
        uint blood = ReadAttributeUInt32(source.Character.Attributes, RuntimeLegacyBloodTideHitpointsAttribute);
        if (HasLegacyStatusFlag(source.Character, LegacyBloodTideFlag)) hp.AddMaximum(blood);
        hp.AddCurrent(blood); // The real setter is unconditional, including a retained field after flag removal.
        if (HasLegacyStatusFlag(source.Character, LegacyEquipmentQueryRampageFlag))
        { hp.AddMaximum(120_000); hp.AddCurrent(hp.Maximum); }
        if (HasLegacyStatusFlag(source.Character, LegacySacrificeFlag) && data is not null &&
            TryGetLegacyEligibleEquippedRuneLevel(source, data, "Sacrifice", out _) &&
            LegacyMagicRuntime.TryResolveLearnedSpell(source.PlayerSpells, data, LegacySacrificeAttachStatusSpellId, out _, out var sacrifice))
        {
            metadata.RequireRuntimeSpell(sacrifice);
            uint power = 0;
            if (LegacyMagicResourceCostCalculator.TryGetEquippedRuneItem(source.PlayerItems, data.ItemsById, "Sacrifice",
                    source.AlternateEquipmentActive, source.Character.Attributes, source.Character.Class, out var rune) && rune is { ItemId: 4_010_701 })
                power = rune.SocketProgress;
            hp.ReduceMaximum(Math.Min(power, sacrifice.Damage));
        }
        if (HasLegacyStatusFlag(source.Character, LegacyFineRain1Flag))
            hp.AddMaximum(ReadAttributeUInt32(source.Character.Attributes, RuntimeLegacyFineRainPowerAttribute));
        if (HasLegacyStatusFlag(source.Character, LegacyRiseOfTaoismFlag))
        { hp.AddMaximum(50_000); hp.AddCurrent(50_000); }
        hp.ClampCurrent(); // This signed cast/clamp happens before an absolute transform maximum.
        if (LegacyTransformState.TryGetActiveMaxHitpoints(source.Character, out var transformed)) hp.Transform(transformed);
        return hp;
    }
}
