using WolfConquer.Core.Data;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private sealed record QuestAcceptanceRow(uint Key, Row Row, uint Uid, Row.QuestStatus Status,
        uint Time, uint[] Array, uint[] Values)
    {
        internal bool IsCurrent() => Row.UID == Uid && Row.Status == Status && Row.Time == Time &&
            ReferenceEquals(Row.Intentions, Array) && Array.AsSpan().SequenceEqual(Values);
    }

    private sealed partial class QuestAcceptanceGraph
    {
        private readonly LegacyNativeQuestsRuntime book;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint playerUid;
        private readonly Dictionary<uint, Row> source, accepted;
        private readonly KeyValuePair<uint, Row>[] sourceEntries, acceptedEntries;
        private readonly QuestAcceptanceRow[] rows;
        private readonly Dictionary<uint, Row> canonical = new();
        private readonly int[] counts;
        private readonly List<uint> kingdom;
        private readonly uint[] kingdomValues;

        internal QuestAcceptanceGraph(LegacyNativeQuestsRuntime book, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player)
        {
            this.book = book; this.client = client; this.player = player; playerUid = player.UID;
            if (!HasOwner()) throw new InvalidDataException("Quest acceptance requires the current physical book.");
            source = book.src ?? throw new InvalidDataException("Quest source index is unavailable.");
            accepted = book.AcceptedQuests ?? throw new InvalidDataException("Quest accepted index is unavailable.");
            if (ReferenceEquals(source, accepted)) throw new InvalidDataException("Quest indexes are aliased.");
            kingdom = LegacyNativeQuestInfo.KingDomMissions ??
                throw new InvalidDataException("Kingdom mission policy is unavailable.");
            kingdomValues = kingdom.ToArray();
            sourceEntries = source.ToArray();
            acceptedEntries = accepted.ToArray();
            rows = new QuestAcceptanceRow[sourceEntries.Length];
            counts = new int[rows.Length];
            var physical = new HashSet<Row>(ReferenceEqualityComparer.Instance);
            var arrays = new HashSet<uint[]>(ReferenceEqualityComparer.Instance);
            for (int index = 0; index < rows.Length; index++)
            {
                var pair = sourceEntries[index]; var row = pair.Value;
                if (row is null || pair.Key != row.UID || row.Intentions is null ||
                    !physical.Add(row) || (row.Intentions.Length != 0 && !arrays.Add(row.Intentions)) ||
                    !canonical.TryAdd(pair.Key, row))
                    throw new InvalidDataException("Quest source identity/array ownership is invalid.");
                counts[index] = row.Intentions.Length;
            }
            LegacyNativeRoleQuests.GetProjectedQuestImageLength(counts, addedCount: null);
            for (int index = 0; index < rows.Length; index++)
            {
                var pair = sourceEntries[index]; var row = pair.Value;
                rows[index] = new(pair.Key, row, row.UID, row.Status, row.Time,
                    row.Intentions, (uint[])row.Intentions.Clone());
            }
            var acceptedUids = new HashSet<uint>();
            foreach (var pair in acceptedEntries)
                if (!acceptedUids.Add(pair.Key) || pair.Value is null ||
                    !canonical.TryGetValue(pair.Key, out var row) || !ReferenceEquals(row, pair.Value) ||
                    row.Status != Row.QuestStatus.Accepted)
                    throw new InvalidDataException("Accepted quest is not the canonical accepted source row.");
            if (!IsCurrent()) throw new InvalidDataException("Quest acceptance graph changed during capture.");
        }

        private bool HasOwner() => playerUid != 0 && player.UID == playerUid &&
            ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            ReferenceEquals(book.Player, player) && ReferenceEquals(book.PersistenceOwner, player) &&
            ReferenceEquals(player.QuestGUI, book);

        internal bool IsKingdom(uint uid) => kingdomValues.Contains(uid);
        internal bool TryGetExisting(uint uid, out Row row) => canonical.TryGetValue(uid, out row!);
        internal void ValidateAppendCount(uint count) =>
            LegacyNativeRoleQuests.GetProjectedQuestImageLength(counts, count);

        private bool HasCurrentPolicy()
        {
            if (!ReferenceEquals(LegacyNativeQuestInfo.KingDomMissions, kingdom) ||
                kingdom.Count != kingdomValues.Length) return false;
            for (int index = 0; index < kingdomValues.Length; index++)
                if (kingdom[index] != kingdomValues[index]) return false;
            return true;
        }

        internal bool IsCurrent()
        {
            if (!HasOwner() || !HasCurrentPolicy() || !ReferenceEquals(book.src, source) ||
                !ReferenceEquals(book.AcceptedQuests, accepted) ||
                !MatchesIndex(source, sourceEntries) || !MatchesIndex(accepted, acceptedEntries)) return false;
            foreach (var row in rows) if (!row.IsCurrent()) return false;
            return true;
        }

        private static bool MatchesIndex(Dictionary<uint, Row> index, KeyValuePair<uint, Row>[] expected)
        {
            if (index.Count != expected.Length) return false;
            int offset = 0;
            foreach (var pair in index)
            {
                if (offset >= expected.Length || pair.Key != expected[offset].Key ||
                    !ReferenceEquals(pair.Value, expected[offset].Value)) return false;
                offset++;
            }
            return offset == expected.Length;
        }
    }
}
