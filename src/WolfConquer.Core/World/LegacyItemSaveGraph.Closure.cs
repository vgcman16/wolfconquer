using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyItemSaveGraph
{
    internal bool IsCurrent(PlayerItemSnapshot items)
    {
        if (!ReferenceEquals(source, items)) return false;
        try
        {
            var groups = Stores(items);
            for (var index = 0; index < groups.Length; index++)
                if (!Matches(stores[index].Source, stores[index].Rows, groups[index])) return false;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException)
        {
            return false;
        }
    }

    internal bool TryGetInventory(uint uid, out Closure closure)
    {
        closure = stores[0].Rows.FirstOrDefault(row => row.Source.Uid == uid)!;
        return closure is not null;
    }

    internal bool TryGetWarehouse(uint warehouseId, uint uid, out Closure closure)
    {
        closure = stores[6].Rows.FirstOrDefault(row => row.Source.WarehouseId == warehouseId && row.Source.Uid == uid)!;
        return closure is not null;
    }

    // Detached presentation rows, selected from the captured topology rather than a live list.
    internal PlayerItemSnapshotItem[] CopyWarehouseRowsForPresentation(uint warehouseId) =>
        stores[6].Rows.Where(row => row.Source.WarehouseId == warehouseId)
            .OrderBy(row => row.Source.Uid).Select(row => row.CopyForPersistence()).ToArray();

    internal bool ContainsAny(Closure incoming) => incoming.ContainsAny(uids);
    internal bool HasBytes(ReadOnlySpan<byte> actual) => actual.SequenceEqual(bytes);

    // Only the existing persistence serializer receives this private, detached projection.
    // The published session keeps its unrelated physical containers and item references.
    internal WorldSession WithOwnedItemsForPersistence(WorldSession session) => session with { PlayerItems = ownedImage };

    private static bool Matches(IReadOnlyList<PlayerItemSnapshotItem> expectedList, Closure[] expectedRows,
        IReadOnlyList<PlayerItemSnapshotItem>? actual)
    {
        if (!ReferenceEquals(expectedList, actual) || actual is null || actual.Count != expectedRows.Length) return false;
        for (var index = 0; index < expectedRows.Length; index++)
            if (!expectedRows[index].MatchesSource(actual[index])) return false;
        return actual.Count == expectedRows.Length;
    }

    internal sealed class Closure
    {
        private readonly IReadOnlyList<PlayerItemSnapshotItem> sourceChildren;
        private readonly Closure[] children;
        private readonly PlayerItemSnapshotItem image;
        internal PlayerItemSnapshotItem Source { get; }

        internal Closure(PlayerItemSnapshotItem source, PlayerItemSnapshotItem image,
            IReadOnlyList<PlayerItemSnapshotItem> sourceChildren, Closure[] children)
        {
            Source = source;
            this.image = image;
            this.sourceChildren = sourceChildren;
            this.children = children;
        }

        internal bool MatchesSource(PlayerItemSnapshotItem? actual)
        {
            try
            {
                return ReferenceEquals(Source, actual) && actual is not null &&
                    actual.PerfectionLevel == image.PerfectionLevel && actual.PerfectionProgress == image.PerfectionProgress &&
                    actual.OwnerUid == image.OwnerUid && string.Equals(actual.OwnerName, image.OwnerName, StringComparison.Ordinal) &&
                    string.Equals(actual.Signature, image.Signature, StringComparison.Ordinal) &&
                    Matches(sourceChildren, children, actual.DepositedItems);
            }
            catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException)
            {
                return false;
            }
        }

        // Walk the captured topology, exposing only the already-public source rows.
        // The private copied image/bytes are never handed to caller predicates.
        internal bool AllSourceRows(Func<PlayerItemSnapshotItem, bool> predicate) =>
            predicate(Source) && children.All(child => child.AllSourceRows(predicate));

        internal bool AnySourceRow(Func<PlayerItemSnapshotItem, bool> predicate) =>
            predicate(Source) || children.Any(child => child.AnySourceRow(predicate));

        internal bool ContainsAny(HashSet<uint> uids) => uids.Contains(image.Uid) || children.Any(child => child.ContainsAny(uids));

        internal PlayerItemSnapshotItem CreateInventoryItem() => CopyForPersistence().WithWarehouseId(0, 0);

        internal PlayerItemSnapshotItem CopyForPersistence() =>
            image.WithDepositedItems(children.Select(child => child.CopyForPersistence()).ToArray());

    }
}
