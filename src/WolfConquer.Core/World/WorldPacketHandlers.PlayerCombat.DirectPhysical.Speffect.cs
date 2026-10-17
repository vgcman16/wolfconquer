namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, LegacyPlayerMagicDamageResult? Result)>
        TrySendLegacyDirectPhysicalReceiveSpeffectAttributesAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyPlayerMagicDamageResult result,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        // ReceiveAttack.Player calls groups 2, 3, 4, 5 in order. A send from
        // one group may replace or remove a participant before the next group.
        context = context with { Session = session };
        (session, result) = await TryApplyLegacyReceiveAttackPlayerVenomSpeffectAsync(
                context,
                session,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return (session, null);
        }

        context = context with { Session = session };
        var defenderContext = context with
        {
            Connection = result.ActiveSession.Connection,
            Session = result.UpdatedSession,
        };
        var defenderSession = await TryApplyLegacyReceiveAttackPlayerSolidSpeffectAsync(
                defenderContext,
                result.UpdatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = defenderSession },
            UpdatedSession = defenderSession,
        };
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return (session, null);
        }

        context = context with { Session = session };
        defenderContext = context with
        {
            Connection = result.ActiveSession.Connection,
            Session = result.UpdatedSession,
        };
        defenderSession = await TryApplyLegacyReceiveAttackPlayerBloodthirstSpeffectAsync(
                defenderContext,
                result.UpdatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = defenderSession },
            UpdatedSession = defenderSession,
        };
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return (session, null);
        }

        context = context with { Session = session };
        defenderContext = context with
        {
            Connection = result.ActiveSession.Connection,
            Session = result.UpdatedSession,
        };
        defenderSession = await TryApplyLegacyReceiveAttackPlayerElanSpeffectAsync(
                defenderContext,
                result.UpdatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = defenderSession },
            UpdatedSession = defenderSession,
        };
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return (session, null);
        }

        return (session, result);
    }
}
