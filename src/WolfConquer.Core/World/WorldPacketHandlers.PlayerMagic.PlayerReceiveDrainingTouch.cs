using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyDrainingTouchRollAttribute = "LegacyDrainingTouchRoll";

    private readonly record struct LegacyPlayerReceiveDrainingTouchResult(
        bool Applied,
        bool ResourcesRestored,
        uint MaxHitpoints,
        uint Hitpoints,
        uint Mana)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveDrainingTouchResult TryApplyLegacyPlayerReceiveDrainingTouch(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        var ratePercent = ReadLegacyPlayerReceiveDrainingTouchPercent(attacker);
        if (ratePercent <= 0 ||
            !CheckLegacyPlayerReceiveDrainingTouchRateRoll(attacker, ratePercent))
        {
            return default;
        }

        if (attackerSession is null)
        {
            return new LegacyPlayerReceiveDrainingTouchResult(
                Applied: true,
                ResourcesRestored: false,
                MaxHitpoints: 0,
                Hitpoints: 0,
                Mana: 0);
        }

        var currentHitpoints = CheckedUInt32(updatedAttacker.MinHitPoints, nameof(updatedAttacker.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            updatedAttacker,
            currentHitpoints,
            attackerSession.PlayerItems,
            attackerSession.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            attackerSession.AlternateEquipmentActive);
        var currentMana = CheckedUInt32(updatedAttacker.MinMana, nameof(updatedAttacker.MinMana));
        var maxMana = CheckedUInt32(updatedAttacker.MaxMana, nameof(updatedAttacker.MaxMana));
        var restored = false;
        var newHitpoints = currentHitpoints;
        var newMana = currentMana;
        if (currentHitpoints < maxHitpoints)
        {
            newHitpoints = maxHitpoints;
            restored = true;
        }

        if (currentMana < maxMana)
        {
            newMana = maxMana;
            restored = true;
        }

        if (restored)
        {
            updatedAttacker = updatedAttacker with
            {
                MinHitPoints = newHitpoints,
                MinMana = newMana,
            };
        }

        return new LegacyPlayerReceiveDrainingTouchResult(
            Applied: true,
            restored,
            maxHitpoints,
            newHitpoints,
            newMana);
    }

    private static double ReadLegacyPlayerReceiveDrainingTouchPercent(CharacterSnapshot attacker)
    {
        return TryReadDoubleAttribute(
                attacker.Attributes,
                out var ratePercent,
                "PerfectionStatus.DrainingTouch",
                "LegacyPerfectionStatus.DrainingTouch",
                "DrainingTouch")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveDrainingTouchRateRoll(
        CharacterSnapshot attacker,
        double chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        var roll = TryReadDoubleAttribute(
                attacker.Attributes,
                out var forcedRoll,
                RuntimeLegacyDrainingTouchRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
