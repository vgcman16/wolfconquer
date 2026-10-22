namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static LegacyEquipmentQueryPublicationResult RefreshLegacyEquipmentQueryPublication(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentQueryPublicationScope scope)
    {
        WorldSession? owner = session;
        WorldSession? required = scope.RequiredParticipant?.Session;
        var loss = LegacyEquipmentQueryPublicationLoss.None;
        if (context.Sessions is { } sessions)
        {
            if (!(scope.OwnerNativeClientIdentity is { } ownerIdentity
                    ? LegacyNativeClientPublication.TryCurrent(
                        sessions, context.Connection, scope.OwnerUid, ownerIdentity, out owner)
                    : sessions.TryGet(context.Connection, out owner)) || owner is null ||
                owner.Character.Uid != scope.OwnerUid)
            {
                owner = null;
                loss |= LegacyEquipmentQueryPublicationLoss.OwnerLost;
            }

            if (scope.RequiredParticipant is { } accepted &&
                (!(scope.RequiredNativeClientIdentity is { } requiredIdentity
                    ? LegacyNativeClientPublication.TryCurrent(sessions, accepted.Connection,
                        accepted.Session.Character.Uid, requiredIdentity, out required)
                    : sessions.TryGet(accepted.Connection, out required)) || required is null ||
                 required.Character.Uid != accepted.Session.Character.Uid))
            {
                required = null;
                loss |= LegacyEquipmentQueryPublicationLoss.RequiredParticipantLost;
            }
        }
        else if (owner.Character.Uid != scope.OwnerUid)
        {
            owner = null;
            loss |= LegacyEquipmentQueryPublicationLoss.OwnerLost;
        }

        if (owner is not null && scope.ExpectedItems is { } items &&
            !ReferenceEquals(owner.PlayerItems, items))
        {
            loss |= LegacyEquipmentQueryPublicationLoss.AcceptedItemsChanged;
        }

        return new(owner, required, loss);
    }

    internal static async ValueTask<LegacyEquipmentQueryPublicationResult>
        SendLegacyCurrentStatusHitpointPairAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyEquipmentQueryPublicationScope scope,
            CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        session = current.Session!;
        var uid = CheckedUInt32(scope.OwnerUid, nameof(scope.OwnerUid));
        // Player.SendUpdateHP3755-3761 captures this Max ONCE in MsgUpdate.
        var capturedMaximum = GetLegacyEquipmentRuntimeMaxHitpoints(session, context.WorldData);
        var maximum = new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, capturedMaximum);
        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(uid, [maximum]).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        session = current.Session!;

        // Source appends fresh signed-int HP after the first send; Max is retained.
        var hitpoints = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.Hitpoints, unchecked((ulong)session.Character.MinHitPoints));
        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(uid, [maximum, hitpoints]).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        return RefreshLegacyEquipmentQueryPublication(context, session, scope);
    }

    internal static async ValueTask<LegacyEquipmentQueryPublicationResult>
        AssignLegacyPlayerHitpointsAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyEquipmentQueryPublicationScope scope,
            int value,
            CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        session = current.Session!;
        current = CommitLegacyEquipmentQueryMutation(context, session, scope, owner =>
        {
            // Source HitPoints assignment never changes independent Status.Max.
            if (!LegacyEquipmentHitpointsStatus.TryGetForOwner(owner, out _))
            {
                var previousHitpoints = owner.Character.MinHitPoints > 0
                    ? CheckedUInt32(owner.Character.MinHitPoints, nameof(owner.Character.MinHitPoints)) : 0U;
                owner = owner with
                {
                    EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(scope.OwnerUid,
                        GetLegacyEquipmentRuntimeMaxHitpoints(owner, context.WorldData, previousHitpoints)),
                };
            }
            return owner with { Character = owner.Character with { MinHitPoints = value } };
        });
        if (!current.Completed) return current;
        session = current.Session!;

        // Source setter: team member mutation/fanout precedes its self HP pair.
        current = await SendLegacyAssignedHitpointsTeamInfoAsync(
                context, session, scope, value, cancellationToken)
            .ConfigureAwait(false);
        if (!current.Completed) return current;
        // Runtime WorldSession is the source post-FullLoading boundary. Loading
        // snapshot helpers do not enter this operation; no invented alias/flag.
        return await SendLegacyCurrentStatusHitpointPairAsync(
                context, current.Session!, scope, cancellationToken)
            .ConfigureAwait(false);
    }
}
