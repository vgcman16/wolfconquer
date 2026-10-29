using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyBoundHitpointsScalarWrite
{
    // Closed observation of the actual monster owner. Acquire the existing
    // prepared Character file gate BEFORE the nonblocking monster gate. The
    // unchanged concrete writer reenters this same file gate; no caller action,
    // saver, validator or acceptance callback executes here.
    internal Outcome TryCommitObservedMonster(MonsterRuntimeStateStore.ClassicBuffTarget target,
        out MonsterRuntimeStateStore.ClassicBuffLease? lease, out string reason)
    {
        MonsterRuntimeStateStore.ClassicBuffLease? acquired = null;
        var failure = string.Empty;
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!target.TryEnter(out acquired, out failure)) return Outcome.Rejected;
                return TryCommit(out failure);
            });
            lease = acquired; reason = failure; return outcome;
        }
        catch
        {
            acquired?.Dispose(); throw;
        }
    }
}
