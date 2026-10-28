using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    internal sealed class PreparedDetachedSessionSpells
    {
        internal LegacyNativeSpellStorage Storage { get; }
        internal PreparedSessionSpellMap Plan { get; }
        internal PreparedDetachedSessionSpells(LegacyNativeSpellStorage storage, PreparedSessionSpellMap plan)
        { Storage = storage; Plan = plan; }
    }

    internal static PlayerSpellSnapshot FreezeSessionSnapshot(PlayerSpellSnapshot proposed)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        if (proposed.Spells is CanonicalSessionSpellRows) return proposed;
        return new(new CanonicalSessionSpellRows(FreezeSnapshotRows(proposed)));
    }

    internal static PreparedDetachedSessionSpells PrepareDetachedSessionSnapshot(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, PlayerSpellSnapshot proposed,
        LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(proposed);
        if (!Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
            (admissionOwner is null ? player.UID == 0 : !admissionOwner.BeforeOwnerIsCurrentPlain(client, player)) ||
            client.MySpells is not null || client.OrdinarySpellBinding is not null)
            throw UnavailableNinjaSpells();
        var storage = new LegacyNativeSpellStorage(client);
        lock (storage.mutationGate)
        {
            var plan = new PreparedSessionSpellMap(storage, client, player,
                PlayerSpellSnapshot.Empty, proposed, detached: true, admissionOwner);
            return new(storage, plan);
        }
    }

    internal static PreparedSessionSpellMap PrepareSessionSnapshot(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, PlayerSpellSnapshot expected, PlayerSpellSnapshot proposed)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(proposed);
        var storage = client.MySpells ?? throw UnavailableNinjaSpells();
        if (!Monitor.IsEntered(storage.mutationGate)) throw UnavailableNinjaSpells();
        return new(storage, client, player, expected, proposed, detached: false);
    }

    internal PlayerSpellSnapshot CaptureSessionSnapshot(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player)
    {
        RequireNinjaSpellOwner(client, player, player.UID);
        return SnapshotPhysicalRows(CaptureNinjaSpellRows(ClientSpells));
    }

    private static PlayerSpellSnapshotEntry[] FreezeSnapshotRows(PlayerSpellSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var source = snapshot.Spells ?? throw UnavailableNinjaSpells();
        int count = source.Count;
        if (count is < 0 or > MaximumPhysicalSpells) throw UnavailableNinjaSpells();
        var frozen = new PlayerSpellSnapshotEntry[count];
        var ids = new HashSet<ushort>();
        for (var index = 0; index < frozen.Length; index++)
        {
            var row = source[index] ?? throw UnavailableNinjaSpells();
            if (row.Id == 0 || row.Experience < 0 || row.UseSpellSoul > 4 || !ids.Add(row.Id))
                throw UnavailableNinjaSpells();
            frozen[index] = new(row.Id, row.Level, row.Experience, row.PreviousLevel, row.SoulLevel, row.UseSpellSoul);
        }
        if (source.Count != count) throw UnavailableNinjaSpells();
        return frozen;
    }

    internal sealed class CanonicalSessionSpellRows : IReadOnlyList<PlayerSpellSnapshotEntry>
    {
        private readonly PlayerSpellSnapshotEntry[] rows;
        internal CanonicalSessionSpellRows(PlayerSpellSnapshotEntry[] source) { rows = source.ToArray(); }
        internal PlayerSpellSnapshotEntry[] CopyRows() => rows.ToArray();
        internal bool MatchesRowsPlain(PlayerSpellSnapshotEntry[] expected)
        {
            if (expected is null || expected.Length != rows.Length) return false;
            for (var index = 0; index < rows.Length; index++)
                if (rows[index] != expected[index]) return false;
            return true;
        }
        public int Count => rows.Length;
        public PlayerSpellSnapshotEntry this[int index] => rows[index];
        public IEnumerator<PlayerSpellSnapshotEntry> GetEnumerator() =>
            ((IEnumerable<PlayerSpellSnapshotEntry>)rows).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => rows.GetEnumerator();
    }

    private static PlayerSpellSnapshot SnapshotPhysicalRows(SpellRow[] rows) => new(new CanonicalSessionSpellRows(rows.Select(row =>
        new PlayerSpellSnapshotEntry(row.Values.Id, row.Values.Level, row.Values.Experience,
            row.Values.PreviousLevel, row.Values.SoulLevel,
            PreparedSessionSpellMap.SelectedSoul(row.Values.UseSoul))).ToArray()));

    private static bool ConsumeFinalWitness(
        ref IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? witness, SpellRow[] rows)
    {
        var current = witness;
        witness = null;
        if (current is null) throw UnavailableNinjaSpells();
        try
        {
            for (var index = 0; index < rows.Length; index++)
            {
                if (!current.MoveNext()) return false;
                var pair = current.Current;
                var saved = rows[index];
                if (pair.Key != saved.Key || !ReferenceEquals(pair.Value, saved.Row) ||
                    ReadSpellValues(pair.Value) != saved.Values) return false;
            }
            return !current.MoveNext();
        }
        finally { current.Dispose(); }
    }
}
