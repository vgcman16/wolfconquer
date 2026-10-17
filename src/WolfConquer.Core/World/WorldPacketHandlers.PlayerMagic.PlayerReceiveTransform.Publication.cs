using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<(WorldSession? Session, bool TargetLost, int ViewerCount,
        LegacyEquipmentQueryOutcome? Query)> PublishLegacyPlayerTransformFinishAsync(
            WorldPacketHandlerContext context,
            LegacyPlayerTransformFinishPreparation prepared,
            CancellationToken cancellationToken,
            WorldActiveSession? acceptedTarget = null)
    {
        var ownerUid = prepared.Session.Character.Uid;
        var scope = new LegacyEquipmentQueryPublicationScope(ownerUid, RequiredParticipant: acceptedTarget);
        var current = RefreshLegacyEquipmentQueryPublication(context, prepared.Session, scope);
        if (!current.Completed) return Result(current, 0, null);

        // ClientTransform.FinishTransform: ID was consumed before this await.
        // Execute ONE actual source query, including rejection and all phases.
        var query = await ExecuteLegacyEquipmentQueryAsync(
                context, current.Session!,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.TransformFinish, scope),
                cancellationToken)
            .ConfigureAwait(false);
        current = new(query.Session, query.RequiredParticipantSession, query.Loss);
        if (!query.Completed) return Result(current, 0, query);
        context = context with { Session = query.Session };

        // Finish's peer spawn is IN ADDITION to the query's admitted appearance.
        var appearance = await SendLegacyCurrentPlayerSpawnToPeersAsync(
                context, query.Session!, scope, cancellationToken)
            .ConfigureAwait(false);
        current = appearance.Current;
        if (!current.Completed) return Result(current, appearance.ViewerCount, query);
        await context.Connection.SendAsync(new MsgActionPacket
            {
                ObjId = CheckedUInt32(ownerUid, nameof(ownerUid)),
                Type = MsgActionType.FinishTransform,
            }.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
        return Result(current, appearance.ViewerCount, query);

        static (WorldSession? Session, bool TargetLost, int ViewerCount, LegacyEquipmentQueryOutcome? Query)
            Result(LegacyEquipmentQueryPublicationResult publication, int viewers, LegacyEquipmentQueryOutcome? query) =>
            (publication.Session,
                (publication.Loss & LegacyEquipmentQueryPublicationLoss.RequiredParticipantLost) != 0,
                viewers, query);
    }

    private static (WorldSession? Owner, WorldActiveSession? Target, bool TargetLost)
        RefreshLegacyTransformFinishParticipants(
            WorldPacketHandlerContext context,
            long ownerUid,
            WorldActiveSession? acceptedTarget)
    {
        var owner = context.Session;
        if (context.Sessions is not { } sessions)
            return owner?.Character.Uid == ownerUid ? (owner, acceptedTarget, false) : (null, null, false);
        if (!sessions.TryGet(context.Connection, out owner) || owner is null || owner.Character.Uid != ownerUid)
            return (null, null, false);
        if (acceptedTarget is null) return (owner, null, false);
        if (!sessions.TryGet(acceptedTarget.Connection, out var target) || target is null ||
            target.Character.Uid != acceptedTarget.Session.Character.Uid)
            return (owner, null, true);
        return (owner, acceptedTarget with { Session = target }, false);
    }
}
