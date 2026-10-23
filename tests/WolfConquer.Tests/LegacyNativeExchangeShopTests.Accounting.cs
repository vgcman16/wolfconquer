using WolfConquer.Core.World;
using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AddAdmitsZeroAndExactLimitRejectsExcessAndTreatsZeroLimitAsUnlimited(bool ex)
    {
        using var state = new State();
        state.Metadata(ex, 17, 17, amount: 3);
        var empty = state.Shop.Items;
        Assert.True(state.Add(ex, 7, 17, 0));
        var zeroBook = state.Shop.Items;
        var zero = Assert.Single(zeroBook).Value;
        Assert.NotSame(empty, zeroBook); Assert.Empty(empty);
        ItemIs(zero, 7, 17, 0);
        Assert.True(state.Add(ex, 7, 17, 3));
        var fullBook = state.Shop.Items;
        var full = fullBook[17];
        Assert.NotSame(zeroBook, fullBook); Assert.NotSame(zero, full);
        ItemIs(full, 7, 17, 3); ItemIs(zero, 7, 17, 0);
        Assert.False(state.Add(ex, 7, 17, 1));
        Assert.Same(fullBook, state.Shop.Items); Assert.Same(full, state.Shop.Items[17]);
        state.Metadata(ex, 19, 19, shop: 8, amount: 2);
        Assert.False(state.Add(ex, 8, 19, 3)); Assert.False(state.Shop.Items.ContainsKey(19));
        state.Metadata(ex, 19, 19, shop: 8, amount: 0);
        Assert.True(state.Add(ex, 8, 19, uint.MaxValue));
        var unlimited = state.Shop.Items[19];
        var unlimitedBook = state.Shop.Items;
        Assert.False(state.Add(ex, 8, 19, 1));
        Assert.Same(unlimitedBook, state.Shop.Items); Assert.Same(unlimited, state.Shop.Items[19]);
        ItemIs(unlimited, 8, 19, uint.MaxValue);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExistingRowOverflowAndWrongPhysicalShopOrGoodsRefuseWithoutChanges(bool ex)
    {
        using var state = new State();
        state.Metadata(ex, 17, 17, shop: 7, amount: 0);
        var row = state.Record(17, 7, 17, uint.MaxValue);
        var book = state.Shop.Items;
        Assert.False(state.Add(ex, 7, 17, 2));
        Assert.False(state.Add(ex, 123, 17, 0));
        Assert.Same(book, state.Shop.Items); Assert.Same(row, book[17]);
        ItemIs(row, 7, 17, uint.MaxValue);
        row.goods_rec_id = 777;
        Assert.False(state.Add(ex, 7, 17, 1));
        Assert.Same(book, state.Shop.Items); ItemIs(row, 7, 777, uint.MaxValue);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingMetadataRejectsBothNewAndExistingRowsWithoutPublishingOrChangingThem(bool ex)
    {
        using var state = new State();
        Assert.False(state.Add(ex, 7, 17, 0)); Assert.Empty(state.Shop.Items);
        var row = state.Record(17, 7, 17, 9);
        var book = state.Shop.Items;
        Assert.False(state.Add(ex, 7, 17, 3));
        Assert.Same(book, state.Shop.Items); Assert.Same(row, book[17]); ItemIs(row, 7, 17, 9);
        state.Shop.Items[19] = null!;
        Assert.False(state.Add(ex, 8, 19, 1)); Assert.Null(state.Shop.Items[19]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidRequiredTablesRowsAndBooksRefuseWithoutSyntheticRecovery(bool ex)
    {
        using var state = new State();
        if (ex) GoodsEx.exchange_shop_goods_ex = null!;
        else Goods.exchange_shop_goods = null!;
        Assert.False(state.Add(ex, 7, 17, 1)); Assert.Empty(state.Shop.Items);
        if (ex) { GoodsEx.exchange_shop_goods_ex = state.ExRows; state.ExRows[17] = null!; }
        else { Goods.exchange_shop_goods = state.GoodsRows; state.GoodsRows[17] = null!; }
        Assert.False(state.Add(ex, 7, 17, 1)); Assert.Empty(state.Shop.Items);
        state.Metadata(ex, 17, 17);
        state.Shop.Items[17] = null!;
        Assert.False(state.Add(ex, 7, 17, 1)); Assert.Null(state.Shop.Items[17]);
        state.Shop.Items = null!;
        Assert.False(state.Add(ex, 7, 17, 1)); Assert.Null(state.Shop.Items);
    }
}
