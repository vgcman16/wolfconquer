using WolfConquer.Core.World;
using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    private sealed record RefreshRow(Shop.Item Row, uint Shop, uint Goods, uint Times);
    private sealed record RefreshCatalogRow(GoodsEx.item Row, uint[] Values);
    private sealed record RefreshGoodsRow(Goods.item Row, uint[] Values);
    private sealed record RefreshFrame(byte[] Reference, byte[] Bytes);
    private sealed record RefreshSnapshot(
        Dictionary<uint, Shop.Item>? Items, KeyValuePair<uint, Shop.Item>[]? Entries,
        RefreshRow[] Rows, Dictionary<uint, GoodsEx.item>? Catalog,
        KeyValuePair<uint, GoodsEx.item>[]? CatalogEntries, RefreshCatalogRow[] CatalogRows,
        Dictionary<uint, Goods.item>? GoodsCatalog, KeyValuePair<uint, Goods.item>[]? GoodsEntries,
        RefreshGoodsRow[] GoodsRows,
        RefreshFrame[] Frames, byte[] PacketBytes, int PacketPosition, int PacketSize,
        LegacyNativeSafeRandom Random);

    private static RefreshSnapshot CaptureRefresh(PacketScope scope, params Shop.Item[] escapedRows)
    {
        var entries = scope.Shop.Items?.ToArray();
        var catalog = GoodsEx.exchange_shop_goods_ex;
        var catalogEntries = catalog?.ToArray();
        var rows = (entries ?? []).Select(pair => pair.Value).Where(row => row is not null)
            .Concat(escapedRows).Distinct(ReferenceEqualityComparer.Instance).Cast<Shop.Item>()
            .Select(row => new RefreshRow(row, row.shopid, row.goods_rec_id, row.times)).ToArray();
        var catalogRows = (catalogEntries ?? []).Select(pair => pair.Value).Where(row => row is not null)
            .Distinct(ReferenceEqualityComparer.Instance).Cast<GoodsEx.item>()
            .Select(row => new RefreshCatalogRow(row, ExValues(row))).ToArray();
        var goods = Goods.exchange_shop_goods;
        var goodsEntries = goods?.ToArray();
        var goodsRows = (goodsEntries ?? []).Select(pair => pair.Value).Where(row => row is not null)
            .Distinct(ReferenceEqualityComparer.Instance).Cast<Goods.item>()
            .Select(row => new RefreshGoodsRow(row, GoodsValues(row))).ToArray();
        return new(scope.Shop.Items, entries, rows, catalog, catalogEntries, catalogRows,
            goods, goodsEntries, goodsRows,
            scope.Queue.Select(frame => new RefreshFrame(frame, frame.ToArray())).ToArray(),
            NativeSpellTestFixture.Bytes(scope.Packet), scope.Packet.Position, scope.Packet.Size,
            LegacyNativePoolRandom.GetRandom);
    }

    private static uint[] ExValues(GoodsEx.item row) =>
        [row.id, row.itemtypeid, row.shopid, row.amount, row.monopoly, row.cost_type,
            row.cost_value, row.weight, row.server_type_flag, row.start_date, row.end_date];

    private static uint[] GoodsValues(Goods.item row) =>
        [row.id, row.itemtypeid, row.shopid, row.amount, row.monopoly, row.material1,
            row.material_amount1, row.material2, row.material_amount2, row.material3,
            row.material_amount3, row.material4, row.material_amount4];

    private static void AssertRefreshStorageUnchanged(RefreshSnapshot before, Shop shop)
    {
        Assert.Same(before.Items, shop.Items);
        AssertDetachedRefreshStorageUnchanged(before);
    }

    private static void AssertDetachedRefreshStorageUnchanged(RefreshSnapshot before)
    {
        AssertEntryIdentity(before.Entries, before.Items?.ToArray());
        foreach (var saved in before.Rows)
            ItemIs(saved.Row, saved.Shop, saved.Goods, saved.Times);
    }

    private static void AssertEntryIdentity<TRow>(KeyValuePair<uint, TRow>[]? expected,
        KeyValuePair<uint, TRow>[]? actual) where TRow : class
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Key, actual[index].Key);
            Assert.Same(expected[index].Value, actual[index].Value);
        }
    }

    private static void AssertRefreshSurroundingsUnchanged(PacketScope scope, RefreshSnapshot before)
    {
        Assert.Same(before.Catalog, GoodsEx.exchange_shop_goods_ex);
        AssertEntryIdentity(before.CatalogEntries, GoodsEx.exchange_shop_goods_ex?.ToArray());
        foreach (var saved in before.CatalogRows) Assert.Equal(saved.Values, ExValues(saved.Row));
        Assert.Same(before.GoodsCatalog, Goods.exchange_shop_goods);
        AssertEntryIdentity(before.GoodsEntries, Goods.exchange_shop_goods?.ToArray());
        foreach (var saved in before.GoodsRows) Assert.Equal(saved.Values, GoodsValues(saved.Row));
        Assert.Same(before.Random, LegacyNativePoolRandom.GetRandom);
        var frames = scope.Queue.ToArray();
        Assert.Equal(before.Frames.Length, frames.Length);
        for (int index = 0; index < frames.Length; index++)
        {
            Assert.Same(before.Frames[index].Reference, frames[index]);
            Assert.Equal(before.Frames[index].Bytes, frames[index]);
        }
        Assert.Equal(before.PacketBytes, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(before.PacketPosition, scope.Packet.Position);
        Assert.Equal(before.PacketSize, scope.Packet.Size);
    }

    private static void AssertRefreshRejected(PacketScope scope, RefreshSnapshot before)
    {
        AssertRefreshStorageUnchanged(before, scope.Shop);
        AssertRefreshSurroundingsUnchanged(scope, before);
    }

    private static Shop.Item ReadyRefreshPrefix(PacketScope scope)
    {
        var old = new Shop.Item { shopid = 7, goods_rec_id = 17, times = 11 };
        scope.Shop.Items.Add(17, old);
        scope.Shop.Items.Add(19, new Shop.Item { shopid = 8, goods_rec_id = 19, times = 13 });
        scope.Shop.Open(scope.Packet, 77, 7);
        Assert.Single(scope.Queue); // Real prior publication, not an empty-output-only control.
        return old;
    }

    private static void ExCatalog(State state, uint shop, uint first, int count)
    {
        for (int index = 0; index < count; index++)
        {
            uint id = checked(first + (uint)index);
            state.ExRows.Add(id, new GoodsEx.item
            {
                id = id, shopid = shop, itemtypeid = checked(700_000u + id), amount = 3,
                monopoly = 1, cost_type = 4, cost_value = 50, weight = 1,
                server_type_flag = 2, start_date = 20_260_101, end_date = 20_261_231,
            });
        }
    }
}
