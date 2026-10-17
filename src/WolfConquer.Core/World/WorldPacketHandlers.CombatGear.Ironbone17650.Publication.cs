using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> SendLegacyCombatGearIronbone17650AnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long attackerUid,
        LegacySpellAnimationPacket animation,
        CancellationToken cancellationToken)
    {
        var frames = animation.ToFrames();
        for (var phase = 0; phase < 2; phase++)
        {
            foreach (var frame in frames)
            {
                if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
                {
                    return session;
                }

                context = context with { Session = session };
                if (phase == 1)
                {
                    await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
                    {
                        return session;
                    }

                    context = context with { Session = session };
                }

                if (context.Sessions is not { } sessions)
                {
                    continue;
                }

                foreach (var candidate in sessions.Snapshot())
                {
                    var target = candidate;
                    var targetUid = target.Session.Character.Uid;
                    if (ReferenceEquals(target.Connection, context.Connection) || targetUid == attackerUid ||
                        !TryRefreshLegacyDirectPhysicalParticipants(
                            context, attackerUid, targetUid, ref session, ref target) ||
                        !CanSeePlayer(session.Character, target.Session.Character) ||
                        ReadCharacterDynamicId(session.Character) != ReadCharacterDynamicId(target.Session.Character))
                    {
                        continue;
                    }

                    await target.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
                    {
                        return session;
                    }

                    context = context with { Session = session };
                }
            }
        }

        return session;
    }
}
