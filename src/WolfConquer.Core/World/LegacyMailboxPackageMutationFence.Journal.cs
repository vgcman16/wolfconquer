namespace WolfConquer.Core.World;

internal static partial class LegacyMailboxPackageMutationFence
{
    [ThreadStatic] private static LegacyMailboxPackageProducer.PreparedJointWork? activeWork;

    private sealed class PreparedScope(LegacyMailboxPackageProducer.PreparedJointWork? previous) : IDisposable
    { public void Dispose() => activeWork = previous; }

    internal static IDisposable EnterPreparedCommit(LegacyMailboxPackageProducer.PreparedJointWork work)
    {
        if (activeWork is not null || !work.IsCurrentPrepared())
            throw new InvalidOperationException("Package journal permission is stale or nested.");
        var previous = activeWork;
        activeWork = work;
        return new PreparedScope(previous);
    }

    internal static void RequireJournalMutation(IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        LegacyOrdinaryAdmissionMutationFence.RequireJournalMutation(paths, changes);
        if (activeWork is not null)
        {
            if (!activeWork.Permits(paths, changes) || !activeWork.IsCurrentPrepared())
                throw new IOException("mailboxClaimRecoveryRequired:packageJournalMismatch");
            return;
        }
        foreach (var path in paths) RequireFileMutation(path);
    }
}
