using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(
        WorldSession Session,
        WorldActiveSession Target,
        LegacyPlayerPlayerPhysicalSpellDamageResult? DamageResult)>
        ApplyLegacyPlayerPlayerPhysicalEffectsAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            LegacyMagicRuntimeSpell? spell,
            LegacyPlayerPlayerPhysicalSpellDamageResult damageResult,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (session, target, null);
        }

        if (context.WorldData is not { } worldData)
        {
            return (session, target, damageResult);
        }

        var mutable = await ApplyLegacyPlayerPlayerPhysicalMutableStatusAsync(
                context, session, target, worldData, attackerUid, targetUid, cancellationToken)
            .ConfigureAwait(false);
        session = mutable.Session;
        target = mutable.Target;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target) ||
            mutable.EnteredCriticalBlock is null)
        {
            return (session, target, null);
        }

        if (mutable.EnteredCriticalBlock is false)
        {
            return (session, target, damageResult);
        }

        context = context with { Session = session };
        var resolution = ResolveLegacyPlayerPlayerPhysicalEffectsCore(session, target.Session, spell, worldData, damageResult);
        if (resolution.RefineEffect != LegacyRefineEffect.None)
        {
            var refineEffect = new LegacyMsgRefineEffectPacket(
                CheckedUInt32(attackerUid, nameof(attackerUid)),
                Param: 0,
                Effect: resolution.RefineEffect);
            var frame = refineEffect.ToFrameBytes();
            await target.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryRefreshLegacyDirectPhysicalParticipants(
                    context, attackerUid, targetUid, ref session, ref target))
            {
                return (session, target, null);
            }

            if (context.Sessions is { } sessions)
            {
                foreach (var candidate in sessions.Snapshot())
                {
                    if (!TryRefreshLegacyDirectPhysicalParticipants(
                            context, attackerUid, targetUid, ref session, ref target))
                    {
                        return (session, target, null);
                    }

                    if (ReferenceEquals(candidate.Connection, target.Connection) ||
                        !sessions.TryGet(candidate.Connection, out var peer) ||
                        peer is null ||
                        peer.Character.Uid != candidate.Session.Character.Uid ||
                        peer.Character.Uid == targetUid ||
                        !CanSeePlayer(target.Session.Character, peer.Character))
                    {
                        continue;
                    }

                    await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    if (!TryRefreshLegacyDirectPhysicalParticipants(
                            context, attackerUid, targetUid, ref session, ref target))
                    {
                        return (session, target, null);
                    }
                }
            }
        }

        return (session, target, resolution.DamageResult);
    }
}
