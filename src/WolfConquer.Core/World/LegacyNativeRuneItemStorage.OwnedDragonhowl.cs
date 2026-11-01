namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    internal sealed partial class BasicPhysicalWornSnapshot
    {
        // Ordinary/basic consumers still require the exact original holder.
        // Only the actual proc's registered publication history can substitute
        // its committed successor; the captured native graph is never refreshed.
        internal bool IsCurrentForDragonhowl(WorldSessionStore.OwnedDragonhowlOwner purpose)
        {
            if (IsCurrentPlain()) return true;
            if (!binding.Store.TryReadDragonhowlWornSuccessor(purpose, binding, this, expected, out var current) ||
                !OwnerIsCurrentPlain(storage, binding, current) || InvertedStorageOrder(binding.Items)) return false;
            var graph = binding.Items;
            lock (graph.Warehouse)
            lock (storage.StorageSyncRoot)
            lock (graph.Relic.SyncRoot)
            lock (graph.Gear.SyncRoot)
                return CurrentUnderStorageGates(current);
        }
    }
}
