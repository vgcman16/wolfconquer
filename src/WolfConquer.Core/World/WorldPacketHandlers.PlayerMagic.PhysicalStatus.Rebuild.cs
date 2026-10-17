using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession PreserveLegacyPhysicalStatusForItemMutation(
        WorldPacketHandlerContext context,
        WorldSession before,
        WorldSession accepted)
    {
        if (before.Character.Uid != accepted.Character.Uid)
        {
            throw new ArgumentException("Item status retention requires the same original UID.", nameof(accepted));
        }

        var fields = CalculateLegacyRuntimeStatusFields(before, context.WorldData);
        return accepted with
        {
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(
                before.Character.Uid, GetLegacyEquipmentRuntimeMaxHitpoints(before, context.WorldData)),
            PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(before) with
            {
                Breakthrough = fields[LegacyPlayerStatusPacket.BreakthroughIndex],
                Counteraction = fields[LegacyPlayerStatusPacket.CounteractionIndex],
            },
        };
    }

    internal static async ValueTask<WorldSession?> RebuildLegacyPhysicalStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentRebuildReason reason,
        CancellationToken cancellationToken)
    {
        var outcome = await ExecuteLegacyEquipmentQueryAsync(
                context, session,
                new LegacyEquipmentQueryRequest(reason,
                    new LegacyEquipmentQueryPublicationScope(session.Character.Uid)),
                cancellationToken)
            .ConfigureAwait(false);
        return outcome.Completed ? outcome.Session : null;
    }

    internal static LegacyEquipmentRebuildResult PrepareLegacyPhysicalStatusRebuild(
        WorldSession session,
        LegacyEquipmentRebuildReason reason,
        ILegacySignedMillisecondClock clock,
        WorldDataCatalog? worldData)
    {
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(session, reason, clock);
        return result.Admitted
            ? result with
            {
                Session = RebuildLegacyEquipmentHitpointsStatus(
                    result.Session, worldData, refillActiveTransform: reason != LegacyEquipmentRebuildReason.PlayerDeath),
            }
            : result;
    }

    internal static bool TryExpireLegacyPhysicalFrontBreak(
        WorldSession session,
        DateTimeOffset now,
        out WorldSession updated,
        out bool removedFlag)
    {
        updated = session;
        removedFlag = false;
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(session);
        if (overlay.FrontBreakExpiresAtUtc is not { } due || now < due)
        {
            return false;
        }

        removedFlag = HasLegacyStatusFlag(session.Character, LegacyPhysicalStatusOverlay.FrontBreakStatusFlag);
        updated = session with
        {
            Character = WithoutLegacyStatusFlag(session.Character, LegacyPhysicalStatusOverlay.FrontBreakStatusFlag),
            PhysicalStatusOverlay = overlay with { FrontBreakExpiresAtUtc = null },
        };
        return true;
    }


}
