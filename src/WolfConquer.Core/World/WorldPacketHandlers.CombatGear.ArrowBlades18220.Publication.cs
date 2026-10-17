namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> SendLegacyArrowBladesVisibleFrameAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid,
        byte[] frame, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        if (context.Sessions is not { } sessions) return session;
        foreach (var candidate in sessions.Snapshot())
        {
            var peer = candidate;
            var peerUid = peer.Session.Character.Uid;
            if (ReferenceEquals(peer.Connection, context.Connection) || peerUid == uid ||
                !TryRefreshLegacyDirectPhysicalParticipants(context, uid, peerUid, ref session, ref peer) ||
                !CanSeePlayer(session.Character, peer.Session.Character) ||
                ReadCharacterDynamicId(session.Character) != ReadCharacterDynamicId(peer.Session.Character)) continue;
            await peer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        }
        return session;
    }

    private static ValueTask<WorldSession?> SendLegacyArrowBladesVectorAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, CancellationToken cancellationToken)
    {
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                session.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        return SendLegacyArrowBladesVisibleFrameAsync(context, session, uid, frame, cancellationToken);
    }

    private static async ValueTask<WorldSession?> RemoveLegacyArrowBladesFlagAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, int flag, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        if (!HasLegacyStatusFlag(session.Character, flag)) return session;
        session = session with
        {
            Character = LegacyArrowBladesFlagRegistration.Forget(WithoutLegacyStatusFlag(session.Character, flag), flag),
        };
        context.Sessions?.Set(context.Connection, session);
        return await SendLegacyArrowBladesVectorAsync(
                context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> ReplaceLegacyArrowBladesFlagAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, int flag, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        // Player.AddSpellFlag silently TryRemoves the old bit, then AddFlag publishes only the NEW vector.
        // No awaited publication or duplicate-AddFlag branch exists between removal and replacement.
        var registration = LegacyArrowBladesFlagRegistration.Create(uid, flag, context.TimeProvider ?? TimeProvider.System);
        session = session with
        {
            Character = registration.Install(WithLegacySourceAddSpellFlag(session.Character, flag, removeOnDead: true)),
        };
        context.Sessions?.Set(context.Connection, session);
        return await SendLegacyArrowBladesVectorAsync(
                context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> SendLegacyArrowBladesOwnerUpdateAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, int flag,
        LegacyMsgUpdateDataType dataType, uint damage, CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        var frame = new LegacyMsgUpdatePacket(CheckedUInt32(uid, nameof(uid)),
            [new LegacyMsgUpdateEntry(dataType, Value: 0, Flag: checked((uint)flag),
                Time: 0, Damage: damage, SpellLevel: 0)]).ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        return TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) ? session : null;
    }
}
