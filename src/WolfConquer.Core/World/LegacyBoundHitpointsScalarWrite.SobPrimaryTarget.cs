using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyBoundHitpointsScalarWrite
{
    // The Character lock precedes the nonblocking actual SOB gate. This is
    // the same prepared scalar writer; no caller supplies an action or saver.
    internal Outcome TryCommitObservedSob(WorldPacketHandlers.LegacySobPrimaryAdmission target,
        out SobNpcRuntimeStateStore.MagicPrimaryLease? lease, out string reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        SobNpcRuntimeStateStore.MagicPrimaryLease? acquired = null;
        var failure = string.Empty;
        var enteredCommit = false;
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!target.TryEnterForCost(store, connection, expected, out acquired, out failure))
                    return Outcome.Rejected;
                enteredCommit = true;
                return TryCommit(out failure);
            });
            lease = acquired; reason = failure; return outcome;
        }
        catch (Exception error)
        {
            Exception commitFailure = error;
            try { acquired?.Dispose(); }
            catch (Exception cleanup) { commitFailure = new AggregateException(error, cleanup); }
            if (enteredCommit) throw new SobPrimaryCommitUncertainException(commitFailure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(commitFailure).Throw();
            throw;
        }
    }

    // Raised only when the actual writer was entered but could not return its
    // concrete outcome. The Store must fence the real owner, never retry.
    internal sealed class SobPrimaryCommitUncertainException(Exception cause)
        : Exception("sobPrimaryCharacterCommitOutcomeUncertain", cause);
}
