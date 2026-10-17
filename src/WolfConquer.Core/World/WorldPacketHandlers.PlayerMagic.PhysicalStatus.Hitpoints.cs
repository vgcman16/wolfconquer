using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint GetLegacyEquipmentRuntimeMaxHitpoints(
        WorldSession session,
        WorldDataCatalog? worldData,
        uint? compatibilityHitpoints = null)
    {
        // Source Status.Max is authoritative even while transformation ID is set:
        // UpdateStatus's HP setter runs BEFORE its later Max assignment.
        if (LegacyEquipmentHitpointsStatus.TryGetForOwner(session, out var cached))
        {
            return cached;
        }

        if (LegacyTransformState.TryGetActiveMaxHitpoints(session.Character, out var transformed))
        {
            return transformed;
        }

        // Compatibility fallback before the first owned query/capture. This is
        // not the raw rebuild path and deliberately preserves pre-existing callers.
        return ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            compatibilityHitpoints ?? CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints)),
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
    }

    private static WorldSession RebuildLegacyEquipmentHitpointsStatus(
        WorldSession session,
        WorldDataCatalog? worldData,
        bool refillActiveTransform = true)
    {
        var maximum = LegacyMaxHitpointsCalculator.CalculateEquipmentRebuildMaxHitpoints(
            session.Character, session.PlayerItems, session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData, session.AlternateEquipmentActive);
        var hitpoints = refillActiveTransform &&
            LegacyTransformState.TryGetActiveMaxHitpoints(session.Character, out _)
            ? (long)maximum // Equip.OnDequeue -> active ClientTransform.UpdateStatus refills.
            : Math.Min(session.Character.MinHitPoints, (long)maximum);
        return session with
        {
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(session.Character.Uid, maximum),
            Character = session.Character with { MinHitPoints = hitpoints },
        };
    }

    private static WorldSession FillLegacyEquipmentHitpointsStatus(
        WorldSession session,
        WorldDataCatalog? worldData) =>
        session with
        {
            Character = session.Character with
            {
                MinHitPoints = GetLegacyEquipmentRuntimeMaxHitpoints(session, worldData),
            },
        };
}
