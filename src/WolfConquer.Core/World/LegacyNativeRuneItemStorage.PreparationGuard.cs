namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    private int storagePreparationDepth;
    private StoragePreparation BeginStoragePreparation() => new(this);

    private readonly struct StoragePreparation : IDisposable
    {
        private readonly LegacyNativeRuneItemStorage owner;
        internal StoragePreparation(LegacyNativeRuneItemStorage owner)
        {
            if (!Monitor.IsEntered(owner.StorageSyncRoot) || owner.storagePreparationDepth != 0)
                throw new InvalidOperationException("Rune preparation is already active or not owned.");
            this.owner = owner; owner.storagePreparationDepth++;
        }
        public void Dispose() => owner.storagePreparationDepth--;
    }
}
