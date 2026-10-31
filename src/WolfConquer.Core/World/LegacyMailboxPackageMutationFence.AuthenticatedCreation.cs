namespace WolfConquer.Core.World;

internal static partial class LegacyMailboxPackageMutationFence
{
    internal static void RequireAuthenticatedCreationJournalMutation(LegacyAuthenticatedOrdinaryCreationProducer producer,
        IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        LegacyOrdinaryAdmissionMutationFence.RequireAuthenticatedCreationJournalMutation(producer, paths, changes);
        foreach (var path in paths) RequirePackageFileMutation(path);
    }
}
