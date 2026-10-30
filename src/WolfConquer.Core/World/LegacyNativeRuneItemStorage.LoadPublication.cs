namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    internal LoadPublication CaptureLoadPublication(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    {
        if (!Monitor.IsEntered(StorageSyncRoot)) throw new InvalidOperationException("Actual Rune gate is required.");
        var binding = new StorageBinding(client, player, player.UID);
        if (!IsCurrent(binding)) throw new InvalidDataException("Rune load owner is foreign.");
        return new LoadPublication(this, client, player);
    }

    internal sealed class LoadPublication
    {
        private readonly LegacyNativeRuneItemStorage owner;
        private readonly StorageBinding binding;
        private readonly StorageSnapshot before;
        internal LegacyNativeGameItemRuntime[] Rows { get; }
        private Dictionary<uint, LegacyNativeGameItemRuntime>? prepared;
        private LegacyNativeGameItemRuntime[]? cache;
        private StorageRow[]? incoming;

        internal LoadPublication(LegacyNativeRuneItemStorage owner, LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
        {
            this.owner = owner; binding = new StorageBinding(client, player, player.UID);
            if (!Monitor.IsEntered(owner.StorageSyncRoot) || owner.storagePreparationDepth != 0 || !owner.IsCurrent(binding))
                throw new InvalidDataException("Rune load capture is not owned.");
            before = owner.RequireStorageSnapshot();
            Rows = before.Rows.Select(row => row.Item).ToArray();
        }

        internal void Prepare(LegacyNativeGameItemRuntime[] additions)
        {
            if (!IsCurrent()) throw new InvalidDataException("Rune load storage changed.");
            using var preparation = owner.BeginStoragePreparation();
            var rows = additions.Select(item => new StorageRow(item, item.Uid, item.Position, item.ITEM_ID)).ToArray();
            var all = before.Rows.ToList();
            var image = new Dictionary<uint, LegacyNativeGameItemRuntime>(checked(all.Count + rows.Length), before.Dictionary.Comparer);
            foreach (var row in all) image.Add(row.Uid, row.Item);
            foreach (var row in rows)
            {
                bool free = row.Position == 212 || (row.Position == 211 && all.Count < 100) ||
                    !all.Any(old => old.Position == row.Position);
                if (row.Uid == 0 || !free || all.Count(old => old.Position == 211) >= 100)
                    throw new InvalidDataException("Rune load admission refused.");
                image.Add(row.Uid, row.Item); all.Add(row);
            }
            var values = image.Values.ToArray();
            prepared = image; cache = values; incoming = rows;
        }

        internal bool IsCurrent()
        {
            if (!Monitor.IsEntered(owner.StorageSyncRoot) || owner.storagePreparationDepth != 0 || !owner.IsCurrent(binding) || !owner.IsCurrentStorage(before)) return false;
            if (incoming is not null)
                foreach (var row in incoming) if (!IsCurrentRow(row)) return false;
            return true;
        }

        internal void PublishUnchecked()
        {
            if (prepared is null) return;
            owner.items = prepared; owner.objects = cache!;
        }
    }
}
