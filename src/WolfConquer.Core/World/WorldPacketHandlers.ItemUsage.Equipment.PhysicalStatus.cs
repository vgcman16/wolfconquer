using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitLegacyEquipmentPhysicalStatusMutation(
        WorldPacketHandlerContext context,
        WorldSession before,
        PlayerItemSnapshot acceptedItems,
        out WorldSession updated)
    {
        updated = before;
        if (!TryRefreshLegacyEquipmentMutationSession(
                context, before.Character.Uid, before.PlayerItems, ref updated))
        {
            return false;
        }

        updated = PreserveLegacyPhysicalStatusForItemMutation(
            context, updated, updated with { PlayerItems = acceptedItems });
        context.Sessions?.Set(context.Connection, updated);
        return true;
    }

    private static bool TryRefreshLegacyEquipmentMutationSession(
        WorldPacketHandlerContext context,
        long ownerUid,
        PlayerItemSnapshot expectedItems,
        ref WorldSession session)
    {
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var current) ||
                current is null || current.Character.Uid != ownerUid ||
                !ReferenceEquals(current.PlayerItems, expectedItems))
            {
                return false;
            }

            session = current;
            return true;
        }

        return session.Character.Uid == ownerUid && ReferenceEquals(session.PlayerItems, expectedItems);
    }

    private static async ValueTask<bool> SendLegacyEquipmentMutationFrameAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long ownerUid,
        PlayerItemSnapshot expectedItems,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session))
        {
            return false;
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        return TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session);
    }

    private static async ValueTask<WorldSession?> CompleteLegacyEquipmentPhysicalStatusMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long ownerUid,
        PlayerItemSnapshot expectedItems,
        string operation,
        bool queryUnconditionally,
        CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session))
        {
            return null;
        }

        // Source query point follows all item frames. Normal Equip and every
        // successful Unequip query; alternate Equip queries only when selected.
        if (queryUnconditionally || session.AlternateEquipmentActive)
        {
            var query = await ExecuteLegacyEquipmentQueryAsync(
                    context with { Session = session }, session,
                    new LegacyEquipmentQueryRequest(
                        LegacyEquipmentRebuildReason.EquipmentMutation,
                        new LegacyEquipmentQueryPublicationScope(ownerUid, expectedItems)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!query.Completed)
            {
                return null;
            }

            session = query.Session!;
            if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session))
            {
                return null;
            }
        }
        else
        {
            // Source2497-2503: this is NOT QueryEquipment/AppendItems.
            if (!await SendLegacyEquipmentMutationFrameAsync(
                    context, session, ownerUid, expectedItems,
                    new LegacyShowEquipmentPacket(
                        WParam: 0,
                        Alternante: 0,
                        Action: LegacyShowEquipmentPacket.AllowAlternanteAction,
                        EquipmentSlots: default).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false) ||
                !TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session))
            {
                return null;
            }
        }

        await TryPersistLegacyItemUsageMutationAsync(
                context with { Session = session }, session, operation,
                characterValues: null, persistItems: true)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, expectedItems, ref session))
        {
            return null;
        }

        // Query owns its source presentation/status/HP/perfection phases. Rejection
        // and inactive-alternate no-query do not authorize a synthetic refresh tail.
        return session;
    }
}
