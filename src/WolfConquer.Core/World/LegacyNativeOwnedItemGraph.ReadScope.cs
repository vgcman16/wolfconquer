namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeOwnedItemGraph
{
    // Read serialization only, never a mutation/publication capability. The
    // actual bound actors must already be held in their established order.
    internal IDisposable EnterCurrentReadScope()
    {
        RequireActors(Client, Player);
        // Taking these locks is not a successful graph check. The caller still
        // runs the existing complete reusable proof while the scope is held.
        return new StorageLease(this);
    }
}
