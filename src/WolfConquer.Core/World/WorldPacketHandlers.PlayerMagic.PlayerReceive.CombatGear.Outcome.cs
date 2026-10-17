using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyPlayerReceiveCombatGearRequest(
        WorldSession AttackerSession,
        WorldActiveSession Target,
        long AttackerUid,
        long TargetUid);

    private readonly record struct LegacyPlayerReceiveCombatGearOutcome(
        WorldSession? AttackerSession,
        WorldActiveSession? Target,
        LegacyPlayerReceiveCombatGearReceipt? Receipt,
        LegacyPlayerMagicReceiveAbortReason AbortReason = LegacyPlayerMagicReceiveAbortReason.None);

    // Private one-receive capability: this proves ONLY completed CombatGear
    // opening for the accepted pair, not speffect/Ninja or damage preparation.
    private sealed class LegacyPlayerReceiveCombatGearReceipt(
        ConquerTcpConnection attackerConnection,
        long attackerUid,
        ConquerTcpConnection targetConnection,
        long targetUid)
    {
        private int consumed;

        public void Consume(
            ConquerTcpConnection actualAttackerConnection,
            long actualAttackerUid,
            ConquerTcpConnection actualTargetConnection,
            long actualTargetUid)
        {
            if (!ReferenceEquals(attackerConnection, actualAttackerConnection) ||
                attackerUid != actualAttackerUid ||
                !ReferenceEquals(targetConnection, actualTargetConnection) ||
                targetUid != actualTargetUid ||
                Interlocked.CompareExchange(ref consumed, 1, 0) != 0)
            {
                throw new InvalidOperationException("CombatGear receipt belongs to one accepted receive pair.");
            }
        }
    }

    private static LegacyPlayerMagicReceiveAbortReason RefreshLegacyPlayerReceiveCombatGearParticipants(
        WorldPacketHandlerContext context,
        LegacyPlayerReceiveCombatGearRequest request,
        ref WorldSession attacker,
        ref WorldActiveSession target)
    {
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var currentAttacker) ||
                currentAttacker is null || currentAttacker.Character.Uid != request.AttackerUid)
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            attacker = currentAttacker;
            if (!sessions.TryGet(request.Target.Connection, out var currentTarget) ||
                currentTarget is null || currentTarget.Character.Uid != request.TargetUid)
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
            target = request.Target with { Session = currentTarget };
        }
        else
        {
            if (attacker.Character.Uid != request.AttackerUid)
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            if (target.Session.Character.Uid != request.TargetUid)
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
        }
        return LegacyPlayerMagicReceiveAbortReason.None;
    }
}
