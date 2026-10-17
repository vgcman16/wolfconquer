using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyBloodSpawnRollAttribute = "LegacyBloodSpawnRoll";

    private readonly record struct LegacyPlayerReceiveBloodSpawnResult(
        bool Applied,
        bool ResourcesRestored,
        uint MaxHitpoints,
        uint Hitpoints,
        uint Mana)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveBloodSpawnResult TryApplyLegacyPlayerReceiveBloodSpawn(
        WorldSession targetSession,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedTarget)
    {
        updatedTarget = targetSession.Character;
        var ratePercent = ReadLegacyPlayerReceiveBloodSpawnPercent(updatedTarget);
        if (ratePercent <= 0 ||
            !CheckLegacyPlayerReceiveBloodSpawnRateRoll(updatedTarget, ratePercent))
        {
            return default;
        }

        var currentHitpoints = CheckedUInt32(updatedTarget.MinHitPoints, nameof(updatedTarget.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            updatedTarget,
            currentHitpoints,
            targetSession.PlayerItems,
            targetSession.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            targetSession.AlternateEquipmentActive);
        var currentMana = CheckedUInt32(updatedTarget.MinMana, nameof(updatedTarget.MinMana));
        var maxMana = CheckedUInt32(updatedTarget.MaxMana, nameof(updatedTarget.MaxMana));
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
            updatedTarget = updatedTarget with
            {
                MinHitPoints = newHitpoints,
                MinMana = newMana,
            };
        }

        return new LegacyPlayerReceiveBloodSpawnResult(
            Applied: true,
            restored,
            maxHitpoints,
            newHitpoints,
            newMana);
    }

    private static double ReadLegacyPlayerReceiveBloodSpawnPercent(CharacterSnapshot target)
    {
        return TryReadDoubleAttribute(
                target.Attributes,
                out var ratePercent,
                "PerfectionStatus.BloodSpawn",
                "LegacyPerfectionStatus.BloodSpawn",
                "BloodSpawn")
            ? ratePercent
            : 0.0;
    }

    private static bool CheckLegacyPlayerReceiveBloodSpawnRateRoll(
        CharacterSnapshot target,
        double chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        var roll = TryReadDoubleAttribute(
                target.Attributes,
                out var forcedRoll,
                RuntimeLegacyBloodSpawnRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
