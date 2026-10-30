namespace WolfConquer.Core.World;

internal partial class LegacyNativeWardrobeRuntime
{
    // This actual graph tag is permanent, including on a retired escaped owner.
    // Only the canonical Store's prepared state publication writes joined containers.
    internal LegacyNativeOwnedItemGraph? JoinedItemGraph;
    internal bool IsJoinedConstructedFor(LegacyNativeGameClientRuntime client, LegacyNativeOwnedItemGraph graph) =>
        ReferenceEquals(constructorOwner, client) && ReferenceEquals(Owner, client) && ReferenceEquals(JoinedItemGraph, graph);
}
