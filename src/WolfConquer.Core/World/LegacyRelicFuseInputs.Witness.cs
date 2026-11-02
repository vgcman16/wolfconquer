using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseInputs
{
    private sealed record Row(PlayerItemSnapshotItem Source, IReadOnlyList<PlayerItemSnapshotItem> Collection,
        int Index, int Group, int Parent, int Depth, IReadOnlyList<PlayerItemSnapshotItem> Deposits, int DepositCount, Values Value);

    private sealed record Graph(IReadOnlyList<PlayerItemSnapshotItem>[] RootSources, int[] RootCounts,
        Row[] Rows, PlayerItemSnapshotItem? First, PlayerItemSnapshotItem? Second, uint MaximumUid, int MaximumDepth)
    {
        internal bool Matches(Graph other)
        {
            if (MaximumUid != other.MaximumUid || MaximumDepth != other.MaximumDepth || Rows.Length != other.Rows.Length ||
                RootSources.Length != other.RootSources.Length) return false;
            for (var index = 0; index < RootSources.Length; index++)
                if (!ReferenceEquals(RootSources[index], other.RootSources[index]) || RootCounts[index] != other.RootCounts[index]) return false;
            for (var index = 0; index < Rows.Length; index++)
            {
                var before = Rows[index]; var current = other.Rows[index];
                if (!ReferenceEquals(before.Source, current.Source) || !ReferenceEquals(before.Collection, current.Collection) ||
                    !ReferenceEquals(before.Deposits, current.Deposits) || before.Index != current.Index || before.Group != current.Group ||
                    before.Parent != current.Parent || before.Depth != current.Depth || before.DepositCount != current.DepositCount ||
                    before.Value != current.Value) return false;
            }
            return true;
        }
    }

    // Other scalar item properties are init-only and the exact item reference
    // must remain current. Capture the fields read by fuse planning and every
    // mutable physical owner/perfection field explicitly, without serialization.
    private readonly record struct Values(uint Uid, uint ItemId, ushort Position, uint WarehouseId, ushort StackSize,
        byte Locked, byte Suspicious, byte Bound, ushort Durability, ushort MaximumDurability,
        uint Activate, uint TimeLeftInMinutes, long Expiration, uint Attribute1, uint Attribute2, uint Attribute3,
        uint Attribute4, uint Attribute5, uint PerfectionLevel, uint PerfectionProgress, uint OwnerUid, string OwnerName, string Signature)
    {
        internal static Values Capture(PlayerItemSnapshotItem item) => new(item.Uid, item.ItemId, item.Position, item.WarehouseId,
            item.StackSize, item.Locked, item.Suspicious, item.Bound, item.Durability, item.MaximumDurability, item.Activate,
            item.TimeLeftInMinutes, item.Expiration, item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5,
            item.PerfectionLevel, item.PerfectionProgress, item.OwnerUid, item.OwnerName, item.Signature);
    }
}
