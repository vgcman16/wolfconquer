using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private sealed record QuestAcceptanceMaps(Dictionary<uint, Row> Source, Dictionary<uint, Row> Accepted,
        KeyValuePair<uint, Row>[] SourceEntries, KeyValuePair<uint, Row>[] AcceptedEntries,
        Row Created, uint Uid, uint Time, uint[] Intentions);

    private sealed partial class QuestAcceptanceGraph
    {
        internal QuestAcceptanceMaps Stage(Row created, bool kingdomOnly)
        {
            var nextSource = new Dictionary<uint, Row>(checked(sourceEntries.Length + 1), source.Comparer);
            // Kingdom source acceptance never adds to the accepted index. Keep
            // that genuine unchanged owner instead of invoking its comparer.
            var nextAccepted = kingdomOnly ? accepted :
                new Dictionary<uint, Row>(checked(acceptedEntries.Length + 1), accepted.Comparer);
            var sourceExpected = AppendEntry(sourceEntries, created);
            var acceptedExpected = kingdomOnly ? (KeyValuePair<uint, Row>[])acceptedEntries.Clone() :
                AppendEntry(acceptedEntries, created);
            foreach (var pair in sourceExpected) nextSource.Add(pair.Key, pair.Value);
            if (!kingdomOnly)
                foreach (var pair in acceptedExpected) nextAccepted.Add(pair.Key, pair.Value);
            var maps = new QuestAcceptanceMaps(nextSource, nextAccepted, sourceExpected, acceptedExpected,
                created, created.UID, created.Time, created.Intentions);
            if (!MatchesPrepared(maps)) throw new InvalidDataException("Quest comparer collapsed or changed staged rows.");
            return maps;
        }

        private static KeyValuePair<uint, Row>[] AppendEntry(KeyValuePair<uint, Row>[] entries, Row created)
        {
            var result = new KeyValuePair<uint, Row>[checked(entries.Length + 1)];
            entries.CopyTo(result, 0);
            result[^1] = new(created.UID, created);
            return result;
        }

        internal bool MatchesPrepared(QuestAcceptanceMaps maps)
        {
            if (!MatchesIndex(maps.Source, maps.SourceEntries) ||
                !MatchesIndex(maps.Accepted, maps.AcceptedEntries) ||
                maps.Created.UID != maps.Uid || maps.Created.Time != maps.Time ||
                maps.Created.Status != Row.QuestStatus.Accepted ||
                !ReferenceEquals(maps.Created.Intentions, maps.Intentions)) return false;
            foreach (uint value in maps.Intentions) if (value != 0) return false;
            foreach (var row in rows) if (!row.IsCurrent()) return false;
            return true;
        }

        internal bool IsCommitted(QuestAcceptanceMaps maps) => HasOwner() && HasCurrentPolicy() &&
            ReferenceEquals(book.src, maps.Source) && ReferenceEquals(book.AcceptedQuests, maps.Accepted) &&
            MatchesPrepared(maps);
    }
}
