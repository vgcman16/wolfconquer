namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Ordinary Store writes use their registered concrete Frame's typed scalar
    // writer. No public callback, loose saved flag or deferred HP mirror exists.
    private bool TryPublishFrameOwnedHitpoints(PreparedSessionSpellPublication publication,
        SessionHolder holder, out string reason)
    {
        reason = string.Empty;
        publication.ArmFinalProof();
        if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
        if (publication.HitpointsWrite is { } write)
        {
            var outcome = write.TryCommit(out reason);
            if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.Rejected) return false;
            if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.RecoveryRequired)
            {
                var failure = "ordinaryHitpointsRecoveryRequired:" + reason;
                var uncertainty = new LegacyNativeHitpointsRecoveryRequiredException(failure);
                try { publication.Binding!.Recovery.Block(failure); }
                catch (Exception fence) { throw new AggregateException("HP persistence outcome and recovery fence failed.", uncertainty, fence); }
                finally { holder.Clear(); }
                throw uncertainty;
            }
        }
        if (!publication.IsCurrentPlain())
        {
            if (publication.HasCommittedHitpoints) FailCommittedSpellPublication(holder, publication.Binding);
            throw InvalidSpellPublication();
        }
        publication.PublishNative();
        publication.PublishHolder();
        return true;
    }
}
