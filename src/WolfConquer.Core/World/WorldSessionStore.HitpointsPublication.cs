namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Existing concrete scalar writer and final proofs; optional closed target
    // observation adds no saver or external validator to this boundary.
    private bool TryPublishFrameOwnedHitpoints(PreparedSessionSpellPublication publication,
        SessionHolder holder, out string reason, PreparedSessionSpellPublication? observedPeer = null,
        SessionHolder? peerHolder = null, MonsterRuntimeStateStore.ClassicBuffTarget? monster = null,
        LegacyRuneCastEligibility? runeEligibility = null)
    {
        reason = string.Empty;
        publication.ArmFinalProof(); observedPeer?.ArmFinalProof();
        if (!publication.ConsumeBeforeWriterPlain() || observedPeer is not null && !observedPeer.ConsumeBeforeWriterPlain())
            throw InvalidSpellPublication();
        if (runeEligibility is not null && !runeEligibility.IsCurrentPlain()) return false;
        MonsterRuntimeStateStore.ClassicBuffLease? lease = null;
        try
        {
            if (publication.HitpointsWrite is { } write)
            {
                var outcome = monster is null ? write.TryCommit(out reason) :
                    write.TryCommitObservedMonster(monster, out lease, out reason);
                if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.Rejected) return false;
                if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.RecoveryRequired)
                {
                    lease?.Dispose(); lease = null;
                    var failure = "ordinaryHitpointsRecoveryRequired:" + reason;
                    var uncertainty = new LegacyNativeHitpointsRecoveryRequiredException(failure);
                    try { publication.Binding!.Recovery.Block(failure); }
                    catch (Exception fence) { throw new AggregateException("HP persistence outcome and recovery fence failed.", uncertainty, fence); }
                    finally { holder.Clear(); peerHolder?.Clear(); }
                    throw uncertainty;
                }
            }
            else if (monster is not null && !monster.TryEnter(out lease, out reason)) return false;
            if (!publication.IsCurrentPlain() || observedPeer is not null && !observedPeer.IsCurrentPlain() ||
                lease is not null && !lease.IsCurrentPlain() || runeEligibility is not null && !runeEligibility.IsCurrentPlain())
            {
                lease?.Dispose(); lease = null;
                if (publication.HasCommittedHitpoints && observedPeer is not null)
                    FailCommittedPairPublication(holder, publication.Binding, peerHolder!, observedPeer.Binding);
                if (publication.HasCommittedHitpoints) FailCommittedSpellPublication(holder, publication.Binding);
                throw InvalidSpellPublication();
            }
            publication.PublishNative(); publication.PublishHolder(); return true;
        }
        finally { lease?.Dispose(); }
    }
}
