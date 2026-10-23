using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoleQuests
{
    private sealed record QuestEntry(uint Key, Row Row, uint Uid, Row.QuestStatus Status,
        uint Time, uint[] Array, uint[] Values)
    {
        internal bool IsCurrent() => Row.UID == Uid && Row.Status == Status && Row.Time == Time &&
            ReferenceEquals(Row.Intentions, Array) && Array.AsSpan().SequenceEqual(Values);
    }

    private sealed class BookGraph
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeQuestsRuntime book;
        private readonly QuestLocation location;
        private readonly Dictionary<uint, Row> source;
        private readonly Dictionary<uint, Row> accepted;
        private readonly KeyValuePair<uint, Row>[] acceptedEntries;
        internal QuestEntry[] Source { get; }
        internal int ImageLength { get; }

        internal BookGraph(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
            LegacyNativeQuestsRuntime book, QuestLocation location)
        {
            this.client = client; this.player = player; this.book = book; this.location = location;
            source = book.src ?? throw new InvalidDataException("Missing source quest map.");
            accepted = book.AcceptedQuests ?? throw new InvalidDataException("Missing accepted quest map.");
            if (ReferenceEquals(source, accepted)) throw new InvalidDataException("Aliased quest indexes.");
            if (source.Count > (MaximumImageBytes - 4) / 16)
                throw new InvalidDataException("Quest book exceeds the image bound.");
            var entries = source.ToArray();
            Source = new QuestEntry[entries.Length];
            var uids = new HashSet<uint>();
            var physicalRows = new HashSet<Row>(ReferenceEqualityComparer.Instance);
            var canonicalRows = new Dictionary<uint, QuestEntry>();
            int length = 4;
            for (int index = 0; index < entries.Length; index++)
            {
                var pair = entries[index];
                var row = pair.Value;
                if (row is null || pair.Key != row.UID || row.Intentions is null ||
                    !uids.Add(row.UID) || !physicalRows.Add(row))
                    throw new InvalidDataException("Invalid or aliased source quest row.");
                var intentions = row.Intentions;
                if (length > MaximumImageBytes - 16 || intentions.Length > (MaximumImageBytes - length - 16) / 4)
                    throw new InvalidDataException("Quest book exceeds the image bound.");
                length = checked(length + 16 + (intentions.Length * 4));
                Source[index] = new(pair.Key, row, row.UID, row.Status, row.Time,
                    intentions, (uint[])intentions.Clone());
                canonicalRows.Add(pair.Key, Source[index]);
            }
            ImageLength = length;
            acceptedEntries = accepted.ToArray();
            var acceptedUids = new HashSet<uint>();
            foreach (var pair in acceptedEntries)
            {
                // Pure physical lookup; the indexes may have custom comparers.
                if (!acceptedUids.Add(pair.Key) || pair.Value is null || !canonicalRows.TryGetValue(pair.Key, out var canonical) ||
                    !ReferenceEquals(pair.Value, canonical.Row) ||
                    canonical.Status != Row.QuestStatus.Accepted)
                    throw new InvalidDataException("Accepted quest is not the canonical source row.");
            }
            if (!IsCurrent()) throw new InvalidDataException("Quest owner changed during capture.");
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
                !ReferenceEquals(player.QuestGUI, book) || !ReferenceEquals(book.Player, player) ||
                !ReferenceEquals(book.PersistenceOwner, player) || !location.IsCurrent(player) ||
                !ReferenceEquals(book.src, source) || !ReferenceEquals(book.AcceptedQuests, accepted) ||
                source.Count != Source.Length || accepted.Count != acceptedEntries.Length) return false;
            int index = 0;
            foreach (var pair in source)
            {
                if (index >= Source.Length) return false;
                var entry = Source[index++];
                if (pair.Key != entry.Key || !ReferenceEquals(pair.Value, entry.Row) || !entry.IsCurrent()) return false;
            }
            if (index != Source.Length) return false;
            index = 0;
            foreach (var pair in accepted)
            {
                if (index >= acceptedEntries.Length) return false;
                var entry = acceptedEntries[index++];
                if (pair.Key != entry.Key || !ReferenceEquals(pair.Value, entry.Value)) return false;
            }
            return index == acceptedEntries.Length;
        }

        internal (Dictionary<uint, Row> Source, Dictionary<uint, Row> Accepted) Merge(Row[] loaded)
        {
            var nextSource = new Dictionary<uint, Row>(source.Comparer);
            var nextAccepted = new Dictionary<uint, Row>(accepted.Comparer);
            foreach (var entry in Source) nextSource.Add(entry.Key, entry.Row);
            foreach (var entry in acceptedEntries) nextAccepted.Add(entry.Key, entry.Value);
            foreach (var row in loaded)
            {
                Row canonical;
                if (nextSource.TryGetValue(row.UID, out var existing))
                {
                    if (!EqualFields(existing, row)) throw new InvalidDataException("Conflicting existing quest UID.");
                    canonical = existing;
                }
                else
                {
                    nextSource.Add(row.UID, row);
                    canonical = row;
                }
                if (row.Status == Row.QuestStatus.Accepted && !nextAccepted.ContainsKey(row.UID))
                    nextAccepted.Add(row.UID, canonical);
            }
            ValidateMergedIndexes(nextSource, nextAccepted, loaded);
            return (nextSource, nextAccepted);
        }

        private static void ValidateMergedIndexes(Dictionary<uint, Row> source,
            Dictionary<uint, Row> accepted, Row[] loaded)
        {
            // Do not trust caller-supplied comparer equivalence as canonical UID identity.
            var physicalSource = new Dictionary<uint, Row>();
            int imageLength = 4;
            foreach (var pair in source)
            {
                if (pair.Value is null || pair.Key != pair.Value.UID || pair.Value.Intentions is null)
                    throw new InvalidDataException("Invalid merged quest identity.");
                if (imageLength > MaximumImageBytes - 16 ||
                    pair.Value.Intentions.Length > (MaximumImageBytes - imageLength - 16) / 4)
                    throw new InvalidDataException("Merged quest book exceeds the image bound.");
                imageLength = checked(imageLength + 16 + (pair.Value.Intentions.Length * 4));
                physicalSource.Add(pair.Key, pair.Value);
            }
            var physicalAccepted = new HashSet<uint>();
            foreach (var pair in accepted)
            {
                if (!physicalSource.TryGetValue(pair.Key, out var canonical) ||
                    !ReferenceEquals(pair.Value, canonical) || canonical.Status != Row.QuestStatus.Accepted)
                    throw new InvalidDataException("Invalid merged accepted alias.");
                if (!physicalAccepted.Add(pair.Key))
                    throw new InvalidDataException("Duplicate physical accepted quest UID.");
            }
            foreach (var row in loaded)
                if (row.Status == Row.QuestStatus.Accepted && !physicalAccepted.Contains(row.UID))
                    throw new InvalidDataException("Accepted comparer collapsed distinct quest UIDs.");
        }

        private static bool EqualFields(Row left, Row right) => left.UID == right.UID &&
            left.Status == right.Status && left.Time == right.Time && left.Intentions is not null &&
            left.Intentions.AsSpan().SequenceEqual(right.Intentions);
    }
}
