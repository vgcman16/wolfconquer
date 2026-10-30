using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedNativeItemImage
{
    // Data/provenance lookup only. The actual Store issues a trade lineage.
    // Inventory roots are the first group in the exact image construction order.
    internal bool TryGetCanonicalInventoryNode(PlayerItemSnapshotItem row, out Node node)
    {
        node = null!;
        var inventory = canonical.Rows[0];
        for (var index = 0; index < inventory.Length; index++)
        {
            if (!ReferenceEquals(inventory[index].Source, row)) continue;
            var candidate = roots[index];
            if (candidate.Record.UID != row.Uid || candidate.Record.Position != 0 || candidate.Record.WH_ID != 0)
                return false;
            node = candidate;
            return true;
        }
        return false;
    }
}
