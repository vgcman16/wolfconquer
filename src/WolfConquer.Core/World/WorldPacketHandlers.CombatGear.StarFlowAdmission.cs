using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Retain this bounded managed entry for existing component callers/tests.
    private static async ValueTask<(WorldSession Session, bool Admitted, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryAdmitLegacyStoneCrackerStarFlowSpellAsync(WorldPacketHandlerContext context, WorldSession session,
            LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        var result = await TryCommitLegacyStarFlowAdmissionAsync(context, session, spell, cancellationToken).ConfigureAwait(false);
        return (result.Owner, result.Admitted, result.AbortReason);
    }

    private static async ValueTask<LegacyCombatProcAdmission> TryCommitLegacyStarFlowAdmissionAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell requested, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uid = session.Character.Uid;
        LegacyCombatProcAdmission Result(bool admitted, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None,
            WorldSessionStore.ProcReceipt? receipt = null) => new(session, null, admitted, reason, receipt);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session))
            return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        if (context.Sessions is null && HasOwnedLegacyProcAttachment(session, context.Connection)) return Result(false);
        if (requested.Id != LegacyCombatGearStarFlowSpellId || context.WorldData is not { } world ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, world, requested.Id, out _, out var spell) ||
            spell != requested ||
            spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack ||
            !TryPrepareLegacyCombatGearProc(context, session, spell, sendMana: true, out var plan)) return Result(false);

        WorldSessionStore.ProcReceipt? committed = null;
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is { } store)
        {
            if (!TryCaptureLegacyProcParticipant(context, session, out var owner))
                return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            try
            {
                if (!store.TryCommitProc(owner, plan.Candidate, null, out committed)) return Result(false);
            }
            catch (LegacyNativeHitpointsRefusalException) { return Result(false); }
            session = committed!.Published;
            if (!await committed.SendAsync(plan.CostFrames, cancellationToken).ConfigureAwait(false))
            {
                if (!committed.TryCurrent(out var interrupted))
                    return Result(true, LegacyPlayerMagicReceiveAbortReason.AttackerLost, committed);
                session = interrupted;
                return Result(false, receipt: committed); // Same-generation committed interruption; outer hit is independent.
            }
            if (!committed.TryCurrent(out var current))
                return Result(true, LegacyPlayerMagicReceiveAbortReason.AttackerLost, committed);
            session = current;
        }
        else
        {
            session = plan.Candidate; // Existing local component path; no invented Store/persistence claim.
            foreach (var frame in plan.CostFrames) await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        // Death after a committed cost notification is a committed interruption
        // of this proc, not a failed preflight or a re-gate of the outer hit.
        if (session.Character.MinHitPoints <= 0) return Result(false, receipt: committed);
        return Result(true, receipt: committed);
    }
}
