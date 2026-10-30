namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Preserve both original private wrappers and their reflected signatures.
    // Only the classic executor selects the closed observed implementation.
    private static ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp, CancellationToken cancellationToken, object? expectedNativeClientIdentity = null) =>
        TryPrepareLegacyPlayerMagicCastObservedAsync(context, session, spell, ignoreAttackStamp,
            cancellationToken, expectedNativeClientIdentity, classicTarget: null);

    private static async ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastObservedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity, LegacyClassicBuffAdmissionRequest? classicTarget)
    {
        if (!TryCaptureLegacyRootAdmissionOwner(
                context, session, expectedNativeClientIdentity, out var owner)) return null;
        session = owner.Expected;
        if (classicTarget is not null)
        {
            if (!TryCaptureLegacyClassicBuffAdmission(context, owner, classicTarget, out var targetReason))
            {
                await context.Log.WriteLineAsync(
                    $"WorldServer skipped classic buff spellId={spell.Id}: {targetReason}." ).ConfigureAwait(false);
                return null;
            }
            owner.ClassicTarget = classicTarget;
        }
        if (spell.Sort == LegacyMagicSort.AttachStatus && spell.Id == LegacyFlyAttachStatusSpellId &&
            IsLegacyFlyBlocked(session))
        {
            await RejectLegacyFlyAdmissionAsync(context, owner, spell, cancellationToken).ConfigureAwait(false);
            return null;
        }
        if (!TryValidateLegacyXpAdmissionPrerequisites(session, spell, out var xpReason))
        {
            await context.Log.WriteLineAsync(
                $"WorldServer skipped legacy player magic spellId={spell.Id}: xpAdmissionRejected={xpReason}.")
                .ConfigureAwait(false);
            return null;
        }
        // Restoration consumes this state even when its priced stamina cost
        // is zero. Validate the actual expected record, protected by its CAS,
        // rather than the caller's potentially older immutable snapshot.
        var requiresRestorationStamina =
            (spell.Sort == LegacyMagicSort.BreathFocus && spell.Id == LegacyBreathFocusSpellId) ||
            ((int)spell.Sort == LegacySwirlingStormSort && spell.Id == LegacySwirlingStormSpellId &&
             HasLegacyStatusFlag(session.Character, LegacyOmnipotenceFlag));
        if (requiresRestorationStamina && !TryReadLegacyRuntimeStamina(session.Character, out _))
        {
            await context.Log.WriteLineAsync(
                $"WorldServer skipped legacy player magic spellId={spell.Id}: requiredStaminaInvalidBeforeAdmission.")
                .ConfigureAwait(false);
            return null;
        }
        // The actual Root invocation already admitted its own, different cost
        // policy. Its executor must not run generic admission a second time.
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return RefreshLegacyRootAcceptedOwner(context, owner, ref session) ? session : null;

        if (!LegacyRuneCastEligibility.TryCapture(context, session, spell, out var runeEligibility)) return null;
        owner.RuneEligibility = runeEligibility;

        LegacyGenericMagicAdmissionPlan plan;
        string reason;
        bool prepared;
        try
        {
            // Preserve this owner's real UTC clock, not Root's local/provider
            // semantics. One instant prices the coherent cadence/cooldown plan.
            prepared = TryPrepareLegacyGenericMagicPlan(
                context, session, spell, ignoreAttackStamp, DateTimeOffset.UtcNow, out plan, out reason);
        }
        catch (Exception error) when (IsLegacyRootPreparationFailure(error))
        {
            plan = null!;
            prepared = false;
            reason = $"preparationInvalid={error.GetType().Name}";
        }
        if (!prepared)
        {
            await context.Log.WriteLineAsync(
                "WorldServer skipped legacy player magic " +
                $"{FormatSession(context)} spellId={spell.Id} {reason}.").ConfigureAwait(false);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!TryCommitLegacyRootAdmission(owner, plan.Candidate, out session))
            {
                if (owner.ClassicTarget is { } refused)
                    await context.Log.WriteLineAsync(
                        $"WorldServer skipped classic buff spellId={spell.Id}: {refused.RefusalReason}." ).ConfigureAwait(false);
                return null;
            }
            if (!IsLegacyRootCommittedRecordCurrent(owner, session)) return null;
            return await PublishLegacyRootAcceptedCostsAsync(
                context, owner, session, plan.Frames, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (owner.CommitAttempted || owner.Committed)
        {
            await ReportLegacyGenericAdmissionFaultAsync(context, owner, error).ConfigureAwait(false);
            throw;
        }
        // This boundary does not admit every target/executor condition. Recruit
        // healing calls the same boundary with its deliberate no-cadence policy.
    }

    private static async ValueTask ReportLegacyGenericAdmissionFaultAsync(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner, Exception error)
    {
        var outcome = owner.Committed ? "committed" : "commit-publication-unproven";
        if (owner.Store is null) outcome = "unbound-local-record-" + outcome;
        try
        {
            await context.Log.WriteLineAsync(
                $"WorldServer generic magic admission {outcome}; no rollback or refund attempted. " +
                $"uid={owner.Expected.Character.Uid} error={error.GetType().Name}.").ConfigureAwait(false);
        }
        catch (Exception diagnostic)
        {
            throw new AggregateException(
                "Generic admission failed and its outcome diagnostic also failed.", error, diagnostic);
        }
    }
}
