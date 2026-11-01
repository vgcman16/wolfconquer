namespace WolfConquer.Core.World;

internal sealed partial class WorldOperationBarrier
{
    internal bool OwnsActiveExclusiveLease(IDisposable? lease) => lease is Lease owned && owned.IsExclusiveOwnedBy(this);
}
