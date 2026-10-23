using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public void AnyRemainderLoadingUsesCanonicalPhysicalGoodsIdentityWithoutRefreshOrRngWhenAlreadyPresent()
    {
        using var state = new State();
        var row = state.Record(168, 999, 168, 7);
        var dictionary = state.Shop.Items;
        var random = LegacyNativePoolRandom.GetRandom;
        Assert.True(state.Shop.TryLoading(out var reason), reason);
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Same(row, Assert.Single(dictionary).Value);
        Assert.Equal(999u, row.shopid); Assert.Equal(168u, row.goods_rec_id); Assert.Equal(7u, row.times);
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.Empty(state.ExRows); Assert.Equal("", state.ConsoleText.ToString());
    }

    [Fact]
    public void AnyRemainderLoadingNullTailRefusesRefreshWithoutPublishingAnyPrefix()
    {
        using var state = new State();
        var row = state.Record(99, 168, 99, 7);
        state.Shop.Items.Add(913, null!);
        var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryLoading(out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Same(dictionary, state.Shop.Items); Assert.Equal(2, dictionary.Count);
        Assert.Same(row, dictionary[99]); Assert.Null(dictionary[913]);
        Assert.Equal(7u, row.times); Assert.Empty(state.ExRows);
    }

    [Fact]
    public void AnyRemainderLoadingMissingBookDoesNotCreateASuccessfulReplacement()
    {
        using var state = new State();
        state.Shop.Items = null!;
        Assert.False(state.Shop.TryLoading(out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Null(state.Shop.Items); Assert.Empty(state.ExRows);
    }

    [Fact]
    public void AnyRemainderLoadingAliasedPhysical168CannotBypassWholeBookAdmission()
    {
        using var state = new State();
        var row = state.Record(912, 999, 168, 7);
        var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryLoading(out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Same(dictionary, state.Shop.Items); Assert.Same(row, Assert.Single(dictionary).Value);
        ItemIs(row, 999, 168, 7);
    }
}
