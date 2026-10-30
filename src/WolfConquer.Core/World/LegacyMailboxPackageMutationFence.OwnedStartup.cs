namespace WolfConquer.Core.World;

internal static partial class LegacyMailboxPackageMutationFence
{
    internal static void RequireOwnedStartupJournalMutation(LegacyTradeJournal.OwnedStartupItemRecovery recovery,
        IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        LegacyOrdinaryAdmissionMutationFence.RequireOwnedStartupJournalMutation(recovery, paths, changes);
        if (activeWork is not null)
        {
            if (!activeWork.Permits(paths, changes) || !activeWork.IsCurrentPrepared())
                throw new IOException("mailboxClaimRecoveryRequired:packageJournalMismatch");
        }
        else
            foreach (var path in paths) RequirePackageFileMutation(path);
        recovery.RequireCurrent();
    }
}
