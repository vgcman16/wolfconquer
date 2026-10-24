using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete save-codec capture only; callers separately own transfer permissions and transactions.
internal sealed partial class LegacyItemSaveGraph
{
    private const int MaximumBytes = CharacterSnapshotFileBaseline.MaximumBytes;
    private sealed record Collection(IReadOnlyList<PlayerItemSnapshotItem> Source, Closure[] Rows);
    private readonly PlayerItemSnapshot source;
    private readonly Collection[] stores;
    private readonly HashSet<uint> uids;
    private readonly PlayerItemSnapshot ownedImage;
    private readonly byte[] bytes;

    private LegacyItemSaveGraph(PlayerItemSnapshot source, Collection[] stores, HashSet<uint> uids,
        PlayerItemSnapshot ownedImage, byte[] bytes)
    {
        this.source = source;
        this.stores = stores;
        this.uids = uids;
        this.ownedImage = ownedImage;
        this.bytes = bytes;
    }

    internal static bool TryCapture(PlayerItemSnapshot items, out LegacyItemSaveGraph capture)
    {
        capture = null!;
        if (items is null) return false;
        try
        {
            var groups = Stores(items);
            var captured = new Collection[groups.Length];
            var seen = new HashSet<uint>();
            long size = 8;
            for (var index = 0; index < groups.Length; index++)
            {
                if (!TryCollection(groups[index], children: false, seen, ref size, out captured[index]) ||
                    captured[index].Rows.Any(row => !ValidPlacement(index, row.Source))) return false;
            }

            PlayerItemSnapshotItem[] Image(int index) => captured[index].Rows.Select(row => row.CopyForPersistence()).ToArray();
            var owned = new PlayerItemSnapshot(Image(0), Image(1), Image(2), Image(3), Image(4), Image(5))
            { WarehouseItems = Image(6) };
            var imageBytes = PlayerItemSnapshotLoader.ToLegacyBytes(owned);
            if (imageBytes.Length != size) return false;
            var result = new LegacyItemSaveGraph(items, captured, seen, owned, imageBytes);
            if (!result.IsCurrent(items)) return false;
            capture = result;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryCollection(IReadOnlyList<PlayerItemSnapshotItem>? rows, bool children,
        HashSet<uint> seen, ref long size, out Collection captured)
    {
        captured = null!;
        if (rows is null) return false;
        var count = rows.Count;
        if (count < 0 || count > (MaximumBytes - size) / PlayerItemSnapshotLoader.LegacyDbItemSize) return false;
        var result = new Closure[count];
        for (var index = 0; index < count; index++)
        {
            var row = rows[index];
            if (row is null || row.Uid == 0 || row.ItemId == 0 || !seen.Add(row.Uid) || row.DepositedItems is null)
                return false;
            // This codec writes one deposited level only. It cannot encode grandchildren.
            if (children && row.DepositedItems.Count != 0) return false;
            size += PlayerItemSnapshotLoader.LegacyDbItemSize;
            if (size > MaximumBytes) return false;
            Collection deposits;
            if (children) deposits = new(row.DepositedItems, []);
            else if (!TryCollection(row.DepositedItems, children: true, seen, ref size, out deposits)) return false;
            var image = row.WithDepositedItems(deposits.Rows.Select(child => child.CopyForPersistence()).ToArray());
            if (!ValidText(image.OwnerName) || !ValidText(image.Signature)) return false;
            if (image.PerfectionLevel != 0 || image.PerfectionProgress != 0 || image.OwnerUid != 0 ||
                image.OwnerName.Length != 0 || image.Signature.Length != 0)
                size += PlayerItemSnapshotLoader.LegacyPerfectionSize;
            if (size > MaximumBytes) return false;
            result[index] = new(row, image, deposits.Source, deposits.Rows);
        }
        if (rows.Count != count) return false;
        captured = new(rows, result);
        return true;
    }

    private static bool ValidText(string text) => text is not null && text.Length <= 32 && !text.Contains('\0') &&
        Encoding.UTF8.GetByteCount(text) <= 32 &&
        string.Equals(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)), text, StringComparison.Ordinal);

    // Exact AddVisibleItem membership; do not parse a lossy image and bless the normalized result.
    private static bool ValidPlacement(int group, PlayerItemSnapshotItem row) => group switch
    {
        0 => row.WarehouseId == 0 && row.Position == 0,
        1 => row.WarehouseId == 0 && row.Position is >= 213 and <= 215,
        2 => row.WarehouseId == 0 && row.Position == 217,
        3 => row.WarehouseId == 0 && row.Position is > 0 and <= 63 ||
            row.WarehouseId == 100 && row.Position is > 0 and <= 59,
        4 => row.WarehouseId == 0 && (row.Position is 211 or 212 or >= 101 and <= 135),
        5 => row.WarehouseId == 100 && row.Position == 0,
        6 => row.WarehouseId != 0 && row.WarehouseId != 100,
        _ => false,
    };

    private static IReadOnlyList<PlayerItemSnapshotItem>[] Stores(PlayerItemSnapshot items) =>
    [items.InventoryItems, items.RelicFuseItems, items.MythsoulBagItems, items.EquipmentItems,
        items.RuneItems, items.WardrobeItems, items.WarehouseItems];
}
