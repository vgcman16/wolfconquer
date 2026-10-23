using Ex = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    // Private staged evidence of the genuine concrete catalog, not an alternate provider.
    private readonly record struct ExGoodsRow(Ex.item Row, uint Id, uint ItemTypeId, uint ShopId,
        uint Amount, uint Monopoly, uint CostType, uint CostValue, uint Weight,
        uint ServerTypeFlag, uint StartDate, uint EndDate);
    private sealed record ExCatalogState(Dictionary<uint, Ex.item> Source, ExGoodsRow[] Rows);

    private static ExGoodsRow CaptureExGoods(Ex.item row) => new(row, row.id, row.itemtypeid,
        row.shopid, row.amount, row.monopoly, row.cost_type, row.cost_value, row.weight,
        row.server_type_flag, row.start_date, row.end_date);

    private static bool TryCaptureExCatalog(out ExCatalogState? catalog, out string reason)
    {
        catalog = null;
        reason = "exchangeShopCatalogUnavailable";
        var source = Ex.exchange_shop_goods_ex;
        if (source is null) return false;
        var entries = source.ToArray();
        var rows = new ExGoodsRow[entries.Length];
        var aliases = new HashSet<Ex.item>(ReferenceEqualityComparer.Instance);
        reason = "exchangeShopCatalogInvalid";
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index];
            if (pair.Value is null || pair.Key != pair.Value.id || !aliases.Add(pair.Value)) return false;
            rows[index] = CaptureExGoods(pair.Value);
        }
        if (!ReferenceEquals(source, Ex.exchange_shop_goods_ex))
        { reason = "exchangeShopStateChanged"; return false; }
        catalog = new(source, rows);
        reason = string.Empty;
        return true;
    }

    private static bool ExCatalogIsCurrent(ExCatalogState catalog)
    {
        if (!ReferenceEquals(catalog.Source, Ex.exchange_shop_goods_ex) ||
            catalog.Source.Count != catalog.Rows.Length) return false;
        foreach (var before in catalog.Rows)
            if (!catalog.Source.TryGetValue(before.Id, out var row) || !ReferenceEquals(row, before.Row) ||
                CaptureExGoods(row) != before) return false;
        return true;
    }

    private static bool HasForeignExCollision(BookState book, ExCatalogState catalog)
    {
        var shops = catalog.Rows.ToDictionary(row => row.Id, row => row.ShopId);
        foreach (var row in book.Rows)
            if (shops.TryGetValue(row.GoodsId, out uint shop) && shop != row.ShopId) return true;
        return false;
    }
}
