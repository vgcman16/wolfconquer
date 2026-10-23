namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    private readonly record struct StorageBinding(
        LegacyNativeGameClientRuntime Client, LegacyNativePlayerRuntime Player, uint Uid);

    private readonly record struct StorageRow(
        LegacyNativeGameItemRuntime Item, uint Uid, ushort Position, uint ItemId);

    private sealed record StorageSnapshot(Dictionary<uint, LegacyNativeGameItemRuntime> Dictionary,
        LegacyNativeGameItemRuntime[] Cache, StorageRow[] Rows);

    private bool IsCurrent(StorageBinding binding) =>
        ReferenceEquals(Owner, binding.Client) && ReferenceEquals(constructorOwner, binding.Client) &&
        ReferenceEquals(binding.Client.Rune, this) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid;

    private StorageSnapshot RequireStorageSnapshot() => TryCaptureStorage(out var snapshot)
        ? snapshot : throw new InvalidDataException("Rune storage is unavailable or has inconsistent physical rows.");

    // Only native storage identity is validated here. Duplicate positions are NOT an Add policy;
    // the equipped owner separately rejects duplicate active slots before granting effects.
    private bool TryCaptureStorage(out StorageSnapshot snapshot)
    {
        snapshot = null!;
        var dictionary = items;
        var cache = objects;
        if (dictionary is null || cache is null || dictionary.Count != cache.Length) return false;
        var entries = dictionary.ToArray();
        var rows = new StorageRow[cache.Length];
        var seen = new HashSet<uint>();
        if (entries.Length != rows.Length) return false;
        for (int index = 0; index < rows.Length; index++)
        {
            var item = cache[index];
            if (item is null || item.Uid == 0 || !seen.Add(item.Uid) ||
                entries[index].Key != item.Uid || !ReferenceEquals(entries[index].Value, item)) return false;
            rows[index] = new(item, item.Uid, item.Position, item.ITEM_ID);
        }
        var captured = new StorageSnapshot(dictionary, cache, rows);
        if (!IsCurrentStorage(captured)) return false;
        snapshot = captured;
        return true;
    }

    // Dictionary enumeration and primitive/reference comparisons do not invoke its comparer.
    // Raw writers outside StorageSyncRoot are not made transactional by these bounded checks.
    private bool IsCurrentStorage(StorageSnapshot snapshot)
    {
        if (!ReferenceEquals(items, snapshot.Dictionary) || !ReferenceEquals(objects, snapshot.Cache) ||
            snapshot.Dictionary.Count != snapshot.Rows.Length || snapshot.Cache.Length != snapshot.Rows.Length)
            return false;
        int index = 0;
        foreach (var entry in snapshot.Dictionary)
        {
            if (index >= snapshot.Rows.Length) return false;
            var row = snapshot.Rows[index];
            if (!ReferenceEquals(snapshot.Cache[index], row.Item) || entry.Key != row.Uid ||
                !ReferenceEquals(entry.Value, row.Item) || !IsCurrentRow(row)) return false;
            index++;
        }
        return index == snapshot.Rows.Length;
    }

    private static bool IsCurrentRow(StorageRow row) => row.Item.Uid == row.Uid &&
        row.Item.Position == row.Position && row.Item.ITEM_ID == row.ItemId;
}
