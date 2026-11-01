using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // DATA preparation uses the same source calculators/selectors. It neither
    // publishes a native query nor substitutes for its later real execution.
    private static void PreflightAttributeQuery(WorldPacketHandlerContext context, WorldSession source,
        LegacyOwnedPhysicalMetadataWitness metadata) =>
        PreflightOwnedEquipmentQuery(context, source, metadata, LegacyEquipmentRebuildReason.AttributeAllocation);

    // Shared closed DATA calculation; the allocation/level-up owners remain separate.
    private static void PreflightOwnedEquipmentQuery(WorldPacketHandlerContext context, WorldSession source,
        LegacyOwnedPhysicalMetadataWitness metadata, LegacyEquipmentRebuildReason reason, bool restoreHitpoints = false)
    {
        if (!LegacyEquipmentRebuildAdmission.ApplyCurrent(source, reason,
                context.LegacyClock ?? LegacySignedMillisecondClock.System).Admitted ||
            !LegacyEquipmentQueryExecution.CanPrepareDerivedSpells(context, source, out _))
            throw new InvalidOperationException("attributeRebuildAdmissionUnavailable");
        var stable = CaptureEquipmentQueryInputs(source);
        var equipment = LegacyEquipmentQuerySelector.Select(stable);
        var hp = PreflightAttributeHitpoints(stable, equipment, context.WorldData, metadata);
        if (restoreHitpoints)
        {
            // UpdateLevel's final setter is signed int, even when Max is UInt32.
            _ = LegacyEquipmentQueryBounds.WithHitpoints(stable.Character, checked((int)hp.Maximum));
            foreach (var packet in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                         checked((uint)stable.Character.Uid), hp.Maximum, hp.Maximum))
                _ = packet.ToFrameBytes();
        }
        PreflightAttributeMagic(context, stable, equipment);
        uint uid = checked((uint)stable.Character.Uid);
        _ = new LegacyPlayerStatusPacket(CalculateLegacyRuntimeStatusFields(stable, context.WorldData)).ToFrameBytes();
        _ = LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, stable.PlayerItems).ToFrameBytes();
        foreach (var packet in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, hp.Maximum, checked((uint)hp.Current)))
            _ = packet.ToFrameBytes();
        var vigor = LegacyEquipmentQueryBounds.ClampVigor(LegacyEquipmentQueryBounds.ReadVigor(stable.Character),
            LegacyMaxHitpointsCalculator.CalculateQueryMaxVigor(equipment, context.WorldData));
        _ = WithEquipmentQueryVigor(stable.Character, vigor);
        _ = LegacyWindWalkerStatusProjection.Read(stable.Character);
    }

    private static uint EquipmentQueryFuryMaximum(byte fury) => fury switch
    {
        >= 1 and <= 11 => 50_000U, 12 => 51_000U, 13 => 52_000U, 14 => 53_000U,
        15 => 54_000U, 16 => 55_000U, 17 => 58_000U, 18 => 60_000U, 19 => 62_000U,
        20 => 65_000U, 21 => 68_000U, 22 => 70_000U, 23 => 75_000U, 24 => 78_000U,
        25 => 80_000U, 26 => 85_000U, 27 => 100_000U, _ => 0U,
    };
}
