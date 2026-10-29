namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Version 2 is one real owner (one Character, one Items target), not a trade
    // record with a fabricated second participant. Both formats share the same
    // pending slot and recovery engine, so a pending sale also fences Trade.
    internal static bool TryCommitSingleOwner(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitSingleOwner(usersRoot, itemsRoot, changes, null, out reason);

    // Closed boundaries: 0 = durable publication; 1 = Character; 2 = Items.
    // StopAfter retains that real prefix for managed restart-recovery controls.
    internal static bool TryCommitSingleOwner(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyJournalInterruption? interruptAfterReplacement, out string reason) =>
        TryCommitVersion(usersRoot, itemsRoot, changes, version: 2, interruptAfterReplacement, out reason);
}
