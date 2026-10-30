namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    // A direct check for one closed recovery invocation, never inherited writer
    // permission. Configured shared-root domains and other owners still deny.
    internal static void RequireOwnedStartupJournalMutation(LegacyTradeJournal.OwnedStartupItemRecovery recovery,
        IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        recovery.RequireExactJournal(paths, changes);
        if (prepared is not null && !prepared.PermitsOrdinaryJournal(paths, changes))
            throw new IOException("ordinaryAdmissionRecoveryRequired:ownedStartupJournalMismatch");
        foreach (var path in paths) RequireFileMutation(path, recovery);
        recovery.RequireCurrent();
    }
}
