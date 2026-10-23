using WolfConquer.Core.Data;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private sealed record ObjectiveRow(uint Key, Row Row, uint Uid, Row.QuestStatus Status,
        uint Time, uint[] Array, uint[] Values)
    {
        internal bool IsCurrent(Row? changed, uint[]? changedArray, uint[]? changedValues) =>
            Row.UID == Uid && Row.Status == Status && Row.Time == Time &&
            (ReferenceEquals(Row, changed)
                ? ReferenceEquals(Row.Intentions, changedArray) && changedArray is not null &&
                    changedValues is not null && changedArray.AsSpan().SequenceEqual(changedValues)
                : ReferenceEquals(Row.Intentions, Array) && Array.AsSpan().SequenceEqual(Values));
    }

    private sealed class ObjectiveGraph
    {
        internal readonly LegacyNativeQuestsRuntime Book;
        internal readonly LegacyNativeGameClientRuntime Client;
        internal readonly LegacyNativePlayerRuntime Player;
        private readonly uint playerUid;
        private readonly Dictionary<uint, Row> source;
        private readonly Dictionary<uint, Row> accepted;
        private readonly ObjectiveRow[] sourceRows;
        private readonly KeyValuePair<uint, Row>[] acceptedRows;
        private readonly Dictionary<uint, ObjectiveRow> canonical = new();
        private readonly HashSet<uint> acceptedUids = new();
        private readonly List<uint> kingdom;
        private readonly uint[] kingdomValues;

        internal ObjectiveGraph(LegacyNativeQuestsRuntime book, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player)
        {
            Book = book; Client = client; Player = player; playerUid = player.UID;
            if (!HasOwner()) throw new InvalidDataException("Quest objectives require the current physical book owner.");
            source = book.src ?? throw new InvalidDataException("Quest source index is unavailable.");
            accepted = book.AcceptedQuests ?? throw new InvalidDataException("Quest accepted index is unavailable.");
            if (ReferenceEquals(source, accepted)) throw new InvalidDataException("Quest indexes are aliased.");
            kingdom = LegacyNativeQuestInfo.KingDomMissions ??
                throw new InvalidDataException("Kingdom mission policy is unavailable.");
            kingdomValues = kingdom.ToArray();
            var entries = source.ToArray();
            sourceRows = new ObjectiveRow[entries.Length];
            var physicalRows = new HashSet<Row>(ReferenceEqualityComparer.Instance);
            var physicalArrays = new HashSet<uint[]>(ReferenceEqualityComparer.Instance);
            for (int index = 0; index < entries.Length; index++)
            {
                var pair = entries[index]; var row = pair.Value;
                if (row is null || pair.Key != row.UID || row.Intentions is null ||
                    !physicalRows.Add(row) || (row.Intentions.Length != 0 && !physicalArrays.Add(row.Intentions)) ||
                    canonical.ContainsKey(pair.Key))
                    throw new InvalidDataException("Quest source identity/array ownership is invalid.");
                var entry = new ObjectiveRow(pair.Key, row, row.UID, row.Status, row.Time,
                    row.Intentions, (uint[])row.Intentions.Clone());
                sourceRows[index] = entry;
                canonical.Add(pair.Key, entry);
            }
            acceptedRows = accepted.ToArray();
            foreach (var pair in acceptedRows)
                if (!acceptedUids.Add(pair.Key) || pair.Value is null ||
                    !canonical.TryGetValue(pair.Key, out var entry) || !ReferenceEquals(entry.Row, pair.Value) ||
                    entry.Status != Row.QuestStatus.Accepted)
                    throw new InvalidDataException("Accepted quest is not the canonical accepted source row.");
            if (!IsCurrent()) throw new InvalidDataException("Quest graph changed during capture.");
        }

        private bool HasOwner() => playerUid != 0 && Player.UID == playerUid &&
            ReferenceEquals(Client.Player, Player) && ReferenceEquals(Player.Owner, Client) &&
            ReferenceEquals(Book.Player, Player) && ReferenceEquals(Book.PersistenceOwner, Player) &&
            ReferenceEquals(Player.QuestGUI, Book);

        internal bool TrySelect(uint uid, ObjectiveMutation operation, out ObjectiveRow? row)
        {
            row = null;
            bool isKingdom = kingdomValues.Contains(uid);
            if (operation == ObjectiveMutation.KingdomSet && !isKingdom) return false;
            if (!canonical.TryGetValue(uid, out var found)) return false;
            bool available = isKingdom && operation != ObjectiveMutation.Set
                ? found.Status != Row.QuestStatus.Finished
                : found.Status == Row.QuestStatus.Accepted && (isKingdom || acceptedUids.Contains(uid));
            if (!available) return false;
            row = found;
            return true;
        }

        internal bool IsCurrent(Row? changed = null, uint[]? changedArray = null, uint[]? changedValues = null)
        {
            if (!HasOwner() || !ReferenceEquals(Book.src, source) || !ReferenceEquals(Book.AcceptedQuests, accepted) ||
                source.Count != sourceRows.Length || accepted.Count != acceptedRows.Length ||
                !ReferenceEquals(LegacyNativeQuestInfo.KingDomMissions, kingdom) || kingdom.Count != kingdomValues.Length)
                return false;
            for (int index = 0; index < kingdomValues.Length; index++)
                if (kingdom[index] != kingdomValues[index]) return false;
            int offset = 0;
            foreach (var pair in source)
            {
                if (offset >= sourceRows.Length) return false;
                var entry = sourceRows[offset++];
                if (pair.Key != entry.Key || !ReferenceEquals(pair.Value, entry.Row) ||
                    !entry.IsCurrent(changed, changedArray, changedValues)) return false;
            }
            if (offset != sourceRows.Length) return false;
            offset = 0;
            foreach (var pair in accepted)
            {
                if (offset >= acceptedRows.Length) return false;
                var entry = acceptedRows[offset++];
                if (pair.Key != entry.Key || !ReferenceEquals(pair.Value, entry.Value)) return false;
            }
            return offset == acceptedRows.Length;
        }
    }
}
