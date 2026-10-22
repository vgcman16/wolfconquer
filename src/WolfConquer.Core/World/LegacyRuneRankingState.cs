using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyRuneRankingEntry(long CharacterUid, string CharacterName, uint Score);

// Handler-lifetime table. Offline rows persist; normalization deliberately drops evicted history.
// Equal scores retain their current order. This is a managed policy, not native runtime tie proof.
public sealed class LegacyRuneRankingState
{
    public const uint MinimumScore = 10_000;
    public const int MaxRankedPlayers = 100;

    private readonly object _gate = new();
    private List<LegacyRuneRankingEntry> _entries;

    public LegacyRuneRankingState(IReadOnlyList<RuneRankingRecord>? seed = null)
    {
        var entries = new List<LegacyRuneRankingEntry>();
        var seen = new HashSet<long>();
        foreach (var row in seed ?? [])
        {
            if (!seen.Add(row.CharacterId))
            {
                throw new ArgumentException("Rune rank seed contains a duplicate character UID.", nameof(seed));
            }

            if (row.Score >= MinimumScore)
            {
                entries.Add(new LegacyRuneRankingEntry(
                    row.CharacterId, row.CharacterName, checked((uint)row.Score)));
            }
        }

        _entries = Normalize(entries);
    }

    public void Update(CharacterSnapshot character, PlayerItemSnapshot items)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(items);
        var entry = new LegacyRuneRankingEntry(
            character.Uid, character.Name, LegacyRuneStoragePacket.ComputeStorageScore(items));

        lock (_gate)
        {
            var index = _entries.FindIndex(candidate => candidate.CharacterUid == entry.CharacterUid);
            if (index < 0)
            {
                _entries.Add(entry);
            }
            else
            {
                _entries[index] = entry;
            }

            _entries = Normalize(_entries);
        }
    }

    public int? FindRank(long characterUid)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.CharacterUid == characterUid);
            return index < 0 ? null : index + 1;
        }
    }

    public IReadOnlyList<LegacyRuneRankingEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    private static List<LegacyRuneRankingEntry> Normalize(IEnumerable<LegacyRuneRankingEntry> entries)
    {
        return entries
            .Where(entry => entry.Score >= MinimumScore)
            .OrderByDescending(entry => entry.Score)
            .Take(MaxRankedPlayers)
            .ToList();
    }
}
