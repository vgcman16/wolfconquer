namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    internal bool MatchesPendingOwner(LegacyOrdinaryAdmissionUidOwner owner, uint id) =>
        false; // Unsafe retained tuning work has no ordinary-login replay authority.

    internal bool IsJournalPrepared => preparedChanges is not null && !committed && !published &&
        storeLease is not null && EntryCurrent() && storeLease.IsCurrentEmpty(connection);

    internal bool PermitsOrdinaryPath(LegacyOrdinaryAdmissionUidOwner owner, uint id, string path) =>
        MatchesPendingOwner(owner, id) && IsJournalPrepared &&
        preparedChanges!.Any(row => string.Equals(roots.PathFor(row.Kind, account), path, StringComparison.OrdinalIgnoreCase));

    internal bool PermitsOrdinaryJournal(IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        if (!IsJournalPrepared || changes.Count != preparedChanges!.Length || paths.Count != changes.Count) return false;
        for (var index = 0; index < changes.Count; index++)
        {
            var actual = changes[index];
            var expected = preparedChanges.SingleOrDefault(row => row.Kind == actual.Kind && row.FileId == actual.FileId);
            if (expected is null || !string.Equals(paths[index], roots.PathFor(actual.Kind, account), StringComparison.OrdinalIgnoreCase) ||
                !Same(expected.Before, actual.Before) || !expected.After.AsSpan().SequenceEqual(actual.After)) return false;
        }
        return true;
    }

    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
}
