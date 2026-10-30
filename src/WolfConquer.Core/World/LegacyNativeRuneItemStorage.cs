namespace WolfConquer.Core.World;

// Native Rune storage with validated, cooperating publication. Item rows remain aliases.
internal sealed partial class LegacyNativeRuneItemStorage
{
    public LegacyNativeGameClientRuntime? Owner;
    private readonly LegacyNativeGameClientRuntime? constructorOwner;
    private Dictionary<uint, LegacyNativeGameItemRuntime> items;
    private LegacyNativeGameItemRuntime[] objects;
    internal object StorageSyncRoot { get; } = new();

    public LegacyNativeRuneItemStorage(LegacyNativeGameClientRuntime? client)
    {
        Owner = client;
        constructorOwner = client;
        items = new();
        objects = [];
    }

    // No caller receives the authoritative cache array. Valid row aliases/order are unchanged.
    public LegacyNativeGameItemRuntime[] Objects
    {
        get
        {
            lock (StorageSyncRoot)
                return (LegacyNativeGameItemRuntime[])RequireStorageSnapshot().Cache.Clone();
        }
    }

    // This is an in-memory cardinality, not a byte wire field.
    public int Count
    {
        get
        {
            lock (StorageSyncRoot)
                return RequireStorageSnapshot().Rows.Length;
        }
    }

    public bool Add(LegacyNativeGameItemRuntime item)
    {
        var client = constructorOwner;
        if (client is null || item is null) return false;
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player;
            if (player is null) return false;
            lock (player.SpellAdmissionSyncRoot)
            lock (StorageSyncRoot)
            {
                if (storagePreparationDepth != 0) return false;
                using var preparation = BeginStoragePreparation();
                var binding = new StorageBinding(client, player, player.UID);
                if (!IsCurrent(binding) || !TryCaptureStorage(out var before)) return false;
                var incoming = new StorageRow(item, item.Uid, item.Position, item.ITEM_ID);
                if (incoming.Uid == 0 || before.Rows.Any(row => row.Uid == incoming.Uid) ||
                    before.Rows.Count(row => row.Position == 211) >= 100) return false;

                // Preserve the genuine dictionary comparer. All potentially throwing allocation/
                // comparer work is detached; failure cannot publish an authoritative-only prefix.
                var prepared = new Dictionary<uint, LegacyNativeGameItemRuntime>(
                    checked(before.Rows.Length + 1), before.Dictionary.Comparer);
                foreach (var row in before.Rows) prepared.Add(row.Uid, row.Item);
                if (!prepared.TryAdd(incoming.Uid, incoming.Item)) return false;
                var cache = prepared.Values.ToArray();

                // No comparer, allocation or callback after these final pure checks.
                if (!IsCurrentStorage(before) || !IsCurrentRow(incoming) || !IsCurrent(binding)) return false;
                items = prepared;
                objects = cache;
                return true;
            }
        }
    }

    // Invalid storage is NOT ordinary absence; a negated query must not authorize mutation.
    public bool TryGetItem(uint uid, out LegacyNativeGameItemRuntime? item)
    {
        lock (StorageSyncRoot)
        {
            item = null;
            item = RequireStorageSnapshot().Rows.FirstOrDefault(row => row.Uid == uid).Item;
            return item is not null;
        }
    }

    public LegacyNativeGameItemRuntime? TryGetItem(byte position)
    {
        lock (StorageSyncRoot)
            return RequireStorageSnapshot().Rows.FirstOrDefault(row => row.Position == position).Item;
    }

    public LegacyNativeGameItemRuntime? TryGetItem(uint id)
    {
        lock (StorageSyncRoot)
            return RequireStorageSnapshot().Rows.FirstOrDefault(row => row.ItemId == id && row.Position != 211).Item;
    }

    public bool ContainsUID(uint uid)
    {
        lock (StorageSyncRoot)
            return RequireStorageSnapshot().Rows.Any(row => row.Uid == uid);
    }

    // Permission query: invalid/missing physical ownership never means a free slot, including212.
    public bool Free(ushort position)
    {
        var client = constructorOwner;
        if (client is null) return false;
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player;
            if (player is null) return false;
            lock (player.SpellAdmissionSyncRoot)
            lock (StorageSyncRoot)
            {
                var binding = new StorageBinding(client, player, player.UID);
                if (!IsCurrent(binding) || !TryCaptureStorage(out var snapshot)) return false;
                bool free = position == 212 || (position == 211 && snapshot.Rows.Length < 100) ||
                    !snapshot.Rows.Any(row => row.Position == position);
                return free && IsCurrentStorage(snapshot) && IsCurrent(binding);
            }
        }
    }
}
