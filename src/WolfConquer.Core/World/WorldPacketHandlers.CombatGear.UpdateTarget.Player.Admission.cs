using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        bool Admitted, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryAdmitLegacyCombatGearNestedPlayerDamageProcAsync(WorldPacketHandlerContext context,
            ConquerTcpConnection attackerConnection, WorldSession attacker, WorldSession defender,
            LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        var result = await TryCommitLegacyNestedProcAdmissionAsync(context, attackerConnection, attacker, defender,
            spell, cancellationToken).ConfigureAwait(false);
        return (result.Owner, result.Peer!, result.Admitted, result.AbortReason);
    }

    private static async ValueTask<LegacyCombatProcAdmission> TryCommitLegacyNestedProcAdmissionAsync(
        WorldPacketHandlerContext context, ConquerTcpConnection attackerConnection, WorldSession attacker,
        WorldSession defender, LegacyMagicRuntimeSpell requested, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attackerUid = attacker.Character.Uid; var defenderUid = defender.Character.Uid;
        LegacyPlayerMagicReceiveAbortReason Refresh() => RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref context, attackerConnection, ref attacker, ref defender, attackerUid, defenderUid);
        LegacyCombatProcAdmission Result(bool admitted, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None,
            WorldSessionStore.ProcReceipt? receipt = null, LegacyNestedProcAuthorization? authorization = null) =>
            new(defender, attacker, admitted, reason, receipt, authorization);
        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
        if (context.Sessions is null && (HasOwnedLegacyProcAttachment(defender, context.Connection) ||
            HasOwnedLegacyProcAttachment(attacker, attackerConnection))) return Result(false);
        if (requested.Id is not (LegacyCombatGearMagneticLightSpellId or LegacyCombatGearAblazeBladeSpellId) ||
            context.WorldData is not { } world || !LegacyMagicRuntime.TryResolveLearnedSpell(defender.PlayerSpells,
                world, requested.Id, out _, out var spell) ||
            spell != requested ||
            !TryPrepareLegacyCombatGearProc(context, defender, spell, sendMana: false, out var plan)) return Result(false);
        if (!CanPrepareLegacyNestedProcTarget(context, defender, attacker, spell, out var effect))
        {
            await SendLegacyNestedProcGateFailureAsync(context, attackerConnection, attacker, defender, effect, cancellationToken)
                .ConfigureAwait(false);
            return Result(false, Refresh());
        }

        var candidate = plan.Candidate; // Independent selected-book/steed entry already ran in the caller.
        cancellationToken.ThrowIfCancellationRequested();
        WorldSessionStore.ProcReceipt? committed = null;
        var authorization = new LegacyNestedProcAuthorization(candidate, attacker);
        if (context.Sessions is { } store)
        {
            if (!TryCaptureLegacyProcParticipant(context, defender, out var owner) ||
                !TryCaptureLegacyProcParticipant(context with { Connection = attackerConnection }, attacker, out var peer))
                return Result(false, Refresh());
            try
            {
                if (!store.TryCommitProc(owner, candidate, peer, out committed)) return Result(false, Refresh());
            }
            catch (LegacyNativeHitpointsRefusalException) { return Result(false); }
            defender = committed!.Published;
            authorization.BindCommitted(defender);
            authorization.Receipt = committed;
            if (!await committed.SendAsync(plan.CostFrames, cancellationToken).ConfigureAwait(false))
            {
                if (!committed.TryCurrent(out var interrupted))
                    return Result(true, LegacyPlayerMagicReceiveAbortReason.TargetLost, committed, authorization);
                defender = interrupted;
                if (!committed.PeerIdentityIsCurrent())
                    return Result(true, LegacyPlayerMagicReceiveAbortReason.AttackerLost, committed, authorization);
                return Result(false, Refresh(), committed, authorization);
            }
            reason = Refresh();
            if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(true, reason, committed, authorization);
        }
        else
        {
            defender = candidate; authorization.BindCommitted(defender);
            foreach (var frame in plan.CostFrames) await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        return Result(true, receipt: committed, authorization: authorization);
    }
}
