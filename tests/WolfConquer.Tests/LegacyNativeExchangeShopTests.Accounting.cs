using WolfConquer.Core.World;
using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAdmitsZeroAndExactLimitRejectsExcessAndTreatsZeroLimitAsUnlimited(bool ex)
    {
        using var state = new State();
        state.Metadata(ex, 17, 17, amount: 3);
        var dictionary = state.Shop.Items;
        Assert.True(state.Add(ex, 7, 17, 0));
        var row = Assert.Single(dictionary).Value;
        ItemIs(row, 7, 17, 0);
        Assert.True(state.Add(ex, 999, 17, 3));
        Assert.False(state.Add(ex, 999, 17, 1));
        Assert.Same(row, state.Shop.Items[17]);
        ItemIs(row, 7, 17, 3);
        state.Metadata(ex, 19, 19, amount: 2);
        Assert.False(state.Add(ex, 8, 19, 3));
        Assert.False(state.Shop.Items.ContainsKey(19));
        state.Metadata(ex, 19, 19, amount: 0);
        Assert.True(state.Add(ex, 8, 19, uint.MaxValue));
        Assert.True(state.Add(ex, 8, 19, 2));
        ItemIs(state.Shop.Items[19], 8, 19, 1);
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Null(typeof(LegacyNativeExchangeShopRuntime)
            .GetField("user", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(state.Shop)); // Accounting does not need a player owner.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingRowWrapsBeforeLimitComparisonAndIgnoresPhysicalShopAndGoodsMismatch(bool ex)
    {
        using var state = new State();
        state.Metadata(ex, 17, 999, shop: 123, amount: 1);
        var row = state.Record(17, 321, 777, uint.MaxValue);
        Assert.True(state.Add(ex, 7, 17, 2));
        Assert.True(state.Add(ex, uint.MaxValue, 17, 0));
        Assert.Same(row, Assert.Single(state.Shop.Items).Value);
        ItemIs(row, 321, 777, 1);
        Assert.False(state.Shop.Items.ContainsKey(777));
        Assert.False(state.Add(ex, 7, 17, 1));
        ItemIs(row, 321, 777, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingMetadataRejectsBothNewAndExistingRowsWithoutPublishingOrChangingThem(bool ex)
    {
        using var state = new State();
        Assert.False(state.Add(ex, 7, 17, 0));
        Assert.Empty(state.Shop.Items);
        var row = state.Record(17, 7, 17, 9);
        Assert.False(state.Add(ex, 8, 17, 3));
        Assert.Same(row, state.Shop.Items[17]);
        ItemIs(row, 7, 17, 9);
        state.Shop.Items[19] = null!;
        Assert.False(state.Add(ex, 8, 19, 1)); // Failed metadata lookup short-circuits the null value.
        Assert.Null(state.Shop.Items[19]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullTablesMetadataRowsAndStoredRowsFaultWithoutSyntheticRecovery(bool ex)
    {
        using var state = new State();
        if (ex) GoodsEx.exchange_shop_goods_ex = null!;
        else Goods.exchange_shop_goods = null!;
        Assert.Throws<NullReferenceException>(() => state.Add(ex, 7, 17, 1));
        Assert.Empty(state.Shop.Items);
        if (ex) { GoodsEx.exchange_shop_goods_ex = state.ExRows; state.ExRows[17] = null!; }
        else { Goods.exchange_shop_goods = state.GoodsRows; state.GoodsRows[17] = null!; }
        Assert.Throws<NullReferenceException>(() => state.Add(ex, 7, 17, 1));
        Assert.Empty(state.Shop.Items);
        state.Metadata(ex, 17, 17);
        state.Shop.Items[17] = null!;
        Assert.Throws<NullReferenceException>(() => state.Add(ex, 7, 17, 1));
        Assert.Null(state.Shop.Items[17]);
        state.Shop.Items = null!;
        Assert.Throws<NullReferenceException>(() => state.Add(ex, 7, 17, 1));
    }
}
