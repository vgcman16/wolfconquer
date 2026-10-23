using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyVictoryQuestCapture
{
    private sealed record CapturedRow(PlayerQuestSnapshotEntry Source, uint Uid,
        PlayerQuestStatus Status, uint Time, IReadOnlyList<uint> Intentions, uint[] Values);

    private static bool TryCaptureRows(IReadOnlyList<PlayerQuestSnapshotEntry> source,
        out CapturedRow[] rows, out int length)
    {
        rows = [];
        length = sizeof(int);
        var count = source.Count;
        if (count < 0 || count > (MaximumImageBytes - length) / PlayerQuestSnapshotLoader.LegacyQuestHeaderSize)
            return false;
        var captured = new CapturedRow[count];
        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var row = source[index];
            if (row is null || row.Uid == 0 || !ValidStatus(row.Status) ||
                !seen.Add(row.Uid) || row.Intentions is null) return false;
            var intentions = row.Intentions;
            var intentionCount = intentions.Count;
            if (length > MaximumImageBytes - PlayerQuestSnapshotLoader.LegacyQuestHeaderSize ||
                intentionCount < 0 || intentionCount >
                    (MaximumImageBytes - length - PlayerQuestSnapshotLoader.LegacyQuestHeaderSize) / sizeof(uint))
                return false;
            length += PlayerQuestSnapshotLoader.LegacyQuestHeaderSize + intentionCount * sizeof(uint);
            var values = new uint[intentionCount];
            for (var value = 0; value < values.Length; value++) values[value] = intentions[value];
            if (intentions.Count != intentionCount) return false;
            captured[index] = new(row, row.Uid, row.Status, row.Time, intentions, values);
        }
        if (source.Count != count) return false;
        rows = captured;
        return true;
    }

    private static bool MatchesRow(CapturedRow expected, PlayerQuestSnapshotEntry actual)
    {
        if (!ReferenceEquals(expected.Source, actual) || actual.Uid != expected.Uid ||
            actual.Status != expected.Status || actual.Time != expected.Time ||
            !ReferenceEquals(expected.Intentions, actual.Intentions) ||
            actual.Intentions.Count != expected.Values.Length) return false;
        for (var index = 0; index < expected.Values.Length; index++)
            if (actual.Intentions[index] != expected.Values[index]) return false;
        return actual.Intentions.Count == expected.Values.Length;
    }

    private static bool MatchesAccepted(IReadOnlyList<PlayerQuestSnapshotEntry> actual,
        PlayerQuestSnapshotEntry[] expected)
    {
        if (actual.Count != expected.Length) return false;
        for (var index = 0; index < expected.Length; index++)
            if (!ReferenceEquals(actual[index], expected[index])) return false;
        return actual.Count == expected.Length;
    }

    private static bool ValidStatus(PlayerQuestStatus status) =>
        status is PlayerQuestStatus.Accepted or PlayerQuestStatus.Finished or PlayerQuestStatus.Available;
}
