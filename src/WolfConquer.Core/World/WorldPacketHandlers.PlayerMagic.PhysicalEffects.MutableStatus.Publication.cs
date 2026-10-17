namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(
        WorldSession Session,
        WorldActiveSession Target,
        bool IdentitiesRetained)>
        SendLegacyPhysicalFrontBreakStatusAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(attackerUid, nameof(attackerUid)),
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    session.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();

        // AddFlag313 -> UpdateFlagOffset is caster-centered and includes self.
        // Commit happened before entry; do not delegate to an unfenced sender.
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (session, target, false);
        }

        if (context.Sessions is { } sessions)
        {
            foreach (var candidate in sessions.Snapshot())
            {
                if (!TryRefreshLegacyDirectPhysicalParticipants(
                        context, attackerUid, targetUid, ref session, ref target))
                {
                    return (session, target, false);
                }

                if (ReferenceEquals(candidate.Connection, context.Connection) ||
                    !sessions.TryGet(candidate.Connection, out var peer) ||
                    peer is null ||
                    peer.Character.Uid != candidate.Session.Character.Uid ||
                    peer.Character.Uid == attackerUid ||
                    !CanSeePlayer(session.Character, peer.Character))
                {
                    continue;
                }

                await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!TryRefreshLegacyDirectPhysicalParticipants(
                        context, attackerUid, targetUid, ref session, ref target))
                {
                    return (session, target, false);
                }
            }
        }

        return (session, target, true);
    }
}
