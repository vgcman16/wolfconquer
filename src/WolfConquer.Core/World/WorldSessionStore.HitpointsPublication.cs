namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Existing concrete scalar writer and final proofs; optional closed target
    // observation adds no saver or external validator to this boundary.
    private bool TryPublishFrameOwnedHitpoints(PreparedSessionSpellPublication publication,
        SessionHolder holder, out string reason, PreparedSessionSpellPublication? observedPeer = null,
        SessionHolder? peerHolder = null, MonsterRuntimeStateStore.ClassicBuffTarget? monster = null,
        LegacyRuneCastEligibility? runeEligibility = null, PreparedBasicPhysicalChange? basicPhysical = null,
        WorldPacketHandlers.OwnedContinuationQueryMutation? continuationQuery = null,
        WorldPacketHandlers.LegacySobPrimaryAdmission? sobPrimary = null)
    {
        reason = string.Empty;
        // Refuse incompatible observations before either consumes an owner proof.
        if (monster is not null && sobPrimary is not null)
        { reason = "ordinaryHitpointsConflictingTargetObservations"; return false; }
        publication.ArmFinalProof(); observedPeer?.ArmFinalProof();
        if (!publication.ConsumeBeforeWriterPlain() || observedPeer is not null && !observedPeer.ConsumeBeforeWriterPlain())
            throw InvalidSpellPublication();
        if (continuationQuery is not null && (publication.Binding is not { } continuationBinding ||
            !continuationQuery.TryConsumeBeforeWriter(continuationBinding))) return false;
        if (runeEligibility is not null && !runeEligibility.IsCurrentPlain()) return false;
        if (basicPhysical is not null && !basicPhysical.IsCurrentPlain()) return false;
        MonsterRuntimeStateStore.ClassicBuffLease? lease = null;
        SobNpcRuntimeStateStore.MagicPrimaryLease? sobLease = null;
        try
        {
            if (publication.HitpointsWrite is { } write)
            {
                LegacyBoundHitpointsScalarWrite.Outcome outcome;
                if (sobPrimary is not null)
                {
                    try { outcome = write.TryCommitObservedSob(sobPrimary, out sobLease, out reason); }
                    catch (LegacyBoundHitpointsScalarWrite.SobPrimaryCommitUncertainException error)
                    {
                        var failure = new LegacyCommittedSpellPublicationException(error);
                        if (observedPeer is not null && peerHolder is not null)
                            FailCommittedPairPublication(holder, publication.Binding, peerHolder, observedPeer.Binding, failure);
                        FailCommittedSpellPublication(holder, publication.Binding, failure);
                        throw;
                    }
                }
                else outcome = monster is null ? write.TryCommit(out reason) :
                    write.TryCommitObservedMonster(monster, out lease, out reason);
                if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.Rejected) return false;
                if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.RecoveryRequired)
                {
                    lease?.Dispose(); lease = null;
                    sobLease?.Dispose(); sobLease = null;
                    var failure = "ordinaryHitpointsRecoveryRequired:" + reason;
                    var uncertainty = new LegacyNativeHitpointsRecoveryRequiredException(failure);
                    try { publication.Binding!.Recovery.Block(failure); }
                    catch (Exception fence) { throw new AggregateException("HP persistence outcome and recovery fence failed.", uncertainty, fence); }
                    finally { holder.Clear(); peerHolder?.Clear(); }
                    throw uncertainty;
                }
            }
            else if (monster is not null && !monster.TryEnter(out lease, out reason)) return false;
            else if (sobPrimary is not null && !sobPrimary.TryEnterForCost(this, publication.Connection,
                publication.ExpectedSession, out sobLease, out reason)) return false;
            if (!publication.IsCurrentPlain() || observedPeer is not null && !observedPeer.IsCurrentPlain() ||
                continuationQuery is not null && (publication.Binding is not { } currentBinding || !continuationQuery.IsConsumedCurrentPlain(currentBinding)) ||
                lease is not null && !lease.IsCurrentPlain() || sobLease is not null && !sobLease.IsCurrentPlain() || runeEligibility is not null && !runeEligibility.IsCurrentPlain())
            {
                lease?.Dispose(); lease = null;
                sobLease?.Dispose(); sobLease = null;
                if (publication.HasCommittedHitpoints && observedPeer is not null)
                    FailCommittedPairPublication(holder, publication.Binding, peerHolder!, observedPeer.Binding);
                if (publication.HasCommittedHitpoints) FailCommittedSpellPublication(holder, publication.Binding);
                throw InvalidSpellPublication();
            }
            if (basicPhysical is not null && !basicPhysical.IsCurrentPlain())
            {
                if (publication.HasCommittedHitpoints) FailCommittedSpellPublication(holder, publication.Binding);
                throw InvalidSpellPublication();
            }
            if (sobPrimary is null)
            {
                publication.PublishNative(); basicPhysical?.PublishPrepared(); publication.PublishHolder(); return true;
            }
            try
            {
                // The actual SOB gate stays held through the real native and
                // holder assignments and their post-publication observation.
                publication.PublishNative(); basicPhysical?.PublishPrepared(); publication.PublishHolder();
                if (sobLease is null || !sobLease.IsCurrentPlain()) throw InvalidSpellPublication();
                return true;
            }
            catch (Exception error)
            {
                sobLease?.Dispose(); sobLease = null;
                var failure = new LegacyCommittedSpellPublicationException(error);
                if (observedPeer is not null && peerHolder is not null)
                    FailCommittedPairPublication(holder, publication.Binding, peerHolder, observedPeer.Binding, failure);
                FailCommittedSpellPublication(holder, publication.Binding, failure);
                throw;
            }
        }
        finally { sobLease?.Dispose(); lease?.Dispose(); }
    }
}
