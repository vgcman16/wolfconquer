using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    private readonly record struct GoodsRow(Goods.item Row, uint Id, uint ItemTypeId, uint ShopId,
        uint Amount, uint Monopoly, uint Material1, uint MaterialAmount1, uint Material2,
        uint MaterialAmount2, uint Material3, uint MaterialAmount3, uint Material4, uint MaterialAmount4);
    private sealed record GoodsCatalogState(Dictionary<uint, Goods.item> Source, GoodsRow[] Rows);

    private static GoodsRow CaptureGoods(Goods.item row) => new(row, row.id, row.itemtypeid,
        row.shopid, row.amount, row.monopoly, row.material1, row.material_amount1,
        row.material2, row.material_amount2, row.material3, row.material_amount3,
        row.material4, row.material_amount4);

    private static bool TryCaptureGoodsCatalog(out GoodsCatalogState? catalog)
    {
        catalog = null;
        var source = Goods.exchange_shop_goods;
        if (source is null) return false;
        var entries = source.ToArray();
        var rows = new GoodsRow[entries.Length];
        var aliases = new HashSet<Goods.item>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index];
            if (pair.Value is null || pair.Key != pair.Value.id || !aliases.Add(pair.Value)) return false;
            rows[index] = CaptureGoods(pair.Value);
        }
        if (!ReferenceEquals(source, Goods.exchange_shop_goods)) return false;
        catalog = new(source, rows);
        return true;
    }

    private static bool GoodsCatalogIsCurrent(GoodsCatalogState catalog)
    {
        if (!ReferenceEquals(catalog.Source, Goods.exchange_shop_goods) ||
            catalog.Source.Count != catalog.Rows.Length) return false;
        foreach (var before in catalog.Rows)
            if (!catalog.Source.TryGetValue(before.Id, out var row) || !ReferenceEquals(row, before.Row) ||
                CaptureGoods(row) != before) return false;
        return true;
    }
}
