namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // This actual graph tag is permanent, including on a retired escaped owner.
    // Only the canonical Store's prepared state publication writes joined containers.
    internal LegacyNativeOwnedItemGraph? JoinedItemGraph;
}
