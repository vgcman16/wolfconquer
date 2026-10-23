using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Snapshot evidence only. The caller owns current-session/CAS and file gates;
// this object neither chooses a quest claim nor grants persistence authority.
internal sealed partial class LegacyVictoryQuestCapture
{
    // Operational serialized-image bound, not a quest/objective gameplay limit.
    internal const int MaximumImageBytes = CharacterSnapshotFileBaseline.MaximumBytes;
    private const string Prefix = "victoryQuest";
    private readonly PlayerQuestSnapshot source;
    private readonly IReadOnlyList<PlayerQuestSnapshotEntry> sourceRows;
    private readonly IReadOnlyList<PlayerQuestSnapshotEntry> sourceAccepted;
    private readonly CapturedRow[] rows;
    private readonly PlayerQuestSnapshotEntry[] acceptedRows;
    private readonly byte[] image;

    private LegacyVictoryQuestCapture(PlayerQuestSnapshot source,
        IReadOnlyList<PlayerQuestSnapshotEntry> sourceRows,
        IReadOnlyList<PlayerQuestSnapshotEntry> sourceAccepted, CapturedRow[] rows,
        PlayerQuestSnapshotEntry[] acceptedRows, byte[] image)
    {
        this.source = source;
        this.sourceRows = sourceRows;
        this.sourceAccepted = sourceAccepted;
        this.rows = rows;
        this.acceptedRows = acceptedRows;
        this.image = image;
    }

    // Never expose the captured mutable backing array to a candidate writer.
    internal byte[] CopyImage() => (byte[])image.Clone();
    internal bool MatchesImage(ReadOnlySpan<byte> candidate) => candidate.SequenceEqual(image);

    internal static bool TryCapture(PlayerQuestSnapshot snapshot,
        out LegacyVictoryQuestCapture capture, out string reason)
    {
        capture = null!;
        reason = Prefix + "SnapshotInvalid";
        if (snapshot is null || snapshot.Quests is null || snapshot.AcceptedQuests is null) return false;
        try
        {
            var sourceRows = snapshot.Quests;
            var sourceAccepted = snapshot.AcceptedQuests;
            if (!TryCaptureRows(sourceRows, out var rows, out var length)) return false;
            var accepted = rows.Where(row => row.Status == PlayerQuestStatus.Accepted)
                .Select(row => row.Source).ToArray();
            if (!MatchesAccepted(sourceAccepted, accepted))
            { reason = Prefix + "AcceptedIndexInvalid"; return false; }

            // Serialize only our bounded private scalar copies, not an escaped
            // collection's enumerator. Retain the real serializer's exact layout.
            var frozenRows = rows.Select(row => new PlayerQuestSnapshotEntry(
                row.Uid, row.Status, row.Time, row.Values)).ToArray();
            var image = PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshot(frozenRows));
            if (image.Length != length) return false;
            var prepared = new LegacyVictoryQuestCapture(snapshot, sourceRows, sourceAccepted,
                rows, accepted, image);
            if (!prepared.IsCurrent(snapshot))
            { reason = Prefix + "SnapshotChanged"; return false; }
            capture = prepared;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsCaptureFailure(error))
        { reason = Prefix + "Capture:" + error.GetType().Name; return false; }
    }

    internal bool IsCurrent(PlayerQuestSnapshot snapshot)
    {
        if (!ReferenceEquals(source, snapshot) || !ReferenceEquals(sourceRows, snapshot.Quests) ||
            !ReferenceEquals(sourceAccepted, snapshot.AcceptedQuests)) return false;
        try
        {
            if (sourceRows.Count != rows.Length) return false;
            for (var index = 0; index < rows.Length; index++)
                if (!MatchesRow(rows[index], sourceRows[index])) return false;
            return sourceRows.Count == rows.Length && MatchesAccepted(sourceAccepted, acceptedRows);
        }
        catch (Exception error) when (IsCaptureFailure(error)) { return false; }
    }

    private static bool IsCaptureFailure(Exception error) => error is ArgumentException or
        InvalidOperationException or NotSupportedException or OverflowException or IndexOutOfRangeException;
}
