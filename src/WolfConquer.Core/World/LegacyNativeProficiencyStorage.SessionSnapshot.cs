using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    // Same defensive byte bound as the existing ordinary journal participant;
    // this is not a proficiency ID or gameplay-level limit.
    private const int MaximumRows = (16 * 1024 * 1024 - sizeof(int)) /
        PlayerProficiencySnapshotLoader.LegacyProficiencySize;
    private readonly object mutationGate = new();
    internal object MutationGate => mutationGate;
    private long mutationGeneration;
    private readonly record struct Values(uint Id, uint Level, uint Experience, uint Needed, byte Previous);
    private readonly record struct Row(uint Key, LegacyNativeMsgProficiency Physical, Values Value);
    private static InvalidOperationException UnavailableProficiencies() =>
        new("Proficiency preparation requires its complete current physical owner and graph.");

    internal static PlayerProficiencySnapshot FreezeSessionSnapshot(PlayerProficiencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Proficiencies is CanonicalRows) return snapshot;
        return new(new CanonicalRows(FreezeRows(snapshot)));
    }

    private static PlayerProficiencySnapshotEntry[] FreezeRows(PlayerProficiencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var source = snapshot.Proficiencies ?? throw UnavailableProficiencies();
        int count = source.Count;
        if (count is < 0 or > MaximumRows) throw UnavailableProficiencies();
        var rows = new PlayerProficiencySnapshotEntry[count];
        var ids = new HashSet<uint>();
        for (int index = 0; index < count; index++)
        {
            var row = source[index] ?? throw UnavailableProficiencies();
            if (row.Id == 0 || !ids.Add(row.Id)) throw UnavailableProficiencies();
            rows[index] = new(row.Id, row.Level, row.PreviousLevel, row.Experience, row.NeededExperience);
        }
        if (source.Count != count) throw UnavailableProficiencies();
        return rows;
    }

    private sealed class CanonicalRows(PlayerProficiencySnapshotEntry[] rows) : IReadOnlyList<PlayerProficiencySnapshotEntry>
    {
        public int Count => rows.Length;
        public PlayerProficiencySnapshotEntry this[int index] => rows[index];
        public IEnumerator<PlayerProficiencySnapshotEntry> GetEnumerator() =>
            ((IEnumerable<PlayerProficiencySnapshotEntry>)rows).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => rows.GetEnumerator();
    }

    internal readonly record struct PreparedDetachedSessionProficiencies(
        LegacyNativeProficiencyStorage Storage, PreparedSessionProficiencyMap Plan);

    internal static PreparedDetachedSessionProficiencies PrepareDetachedSessionSnapshot(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, PlayerProficiencySnapshot proposed,
        LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner = null)
    {
        if (client is null || player is null || client.MyProfs is not null || client.OrdinarySpellBinding is not null ||
            !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
            (admissionOwner is null ? player.UID == 0 : !admissionOwner.BeforeOwnerIsCurrentPlain(client, player)) ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot))
            throw UnavailableProficiencies();
        var storage = new LegacyNativeProficiencyStorage(client);
        lock (storage.mutationGate)
            return new(storage, new(storage, client, player, PlayerProficiencySnapshot.Empty, proposed, detached: true, admissionOwner));
    }

    internal static PreparedSessionProficiencyMap PrepareSessionSnapshot(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, PlayerProficiencySnapshot expected, PlayerProficiencySnapshot proposed)
    {
        var storage = client.MyProfs ?? throw UnavailableProficiencies();
        return new(storage, client, player, expected, proposed, detached: false);
    }

    private static Values ReadValues(LegacyNativeMsgProficiency row) =>
        new(row.ID, row.Level, row.Experience, row.NeededExperience, row.PreviouseLevel);
    private static Values ReadValues(PlayerProficiencySnapshotEntry row) =>
        new(row.Id, row.Level, row.Experience, row.NeededExperience, row.PreviousLevel);
    private static LegacyNativeMsgProficiency CreateRow(PlayerProficiencySnapshotEntry row) => new()
    {
        ID = row.Id, Level = row.Level, Experience = row.Experience,
        NeededExperience = row.NeededExperience, PreviouseLevel = row.PreviousLevel,
    };

    private static Row[] CaptureRows(ConcurrentDictionary<uint, LegacyNativeMsgProficiency> map)
    {
        if (map is null || map.Count > MaximumRows) throw UnavailableProficiencies();
        var rows = new List<Row>();
        var ids = new HashSet<uint>();
        var physical = new HashSet<LegacyNativeMsgProficiency>(ReferenceEqualityComparer.Instance);
        foreach (var pair in map)
        {
            if (rows.Count >= MaximumRows || pair.Value is not { } row || pair.Key == 0 || pair.Key != row.ID ||
                !ids.Add(pair.Key) || !physical.Add(row)) throw UnavailableProficiencies();
            rows.Add(new(pair.Key, row, ReadValues(row)));
        }
        return rows.ToArray();
    }

    private static void RequireRows(ConcurrentDictionary<uint, LegacyNativeMsgProficiency> map, Row[] rows)
    {
        if (map.Count != rows.Length) throw UnavailableProficiencies();
        using var iterator = map.GetEnumerator();
        if (!ConsumeRows(iterator, rows)) throw UnavailableProficiencies();
    }

    private static bool ConsumeRows(IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>> iterator, Row[] rows)
    {
        foreach (var saved in rows)
        {
            if (!iterator.MoveNext()) return false;
            var pair = iterator.Current;
            if (pair.Key != saved.Key || !ReferenceEquals(pair.Value, saved.Physical) ||
                ReadValues(pair.Value) != saved.Value) return false;
        }
        return !iterator.MoveNext();
    }
}
