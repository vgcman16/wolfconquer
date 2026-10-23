using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public void GetRandomExUsesRealSharedRngAndPublishesOnePreparedReplacementWithZeroUses()
    {
        using var state = new State();
        state.Metadata(true, 17, 17, 7, 3);
        state.Metadata(true, 19, 19, 8, 3);
        var old = state.Record(17, 7, 17, 11);
        var other = state.Record(19, 8, 19, 13);
        var dictionary = state.Shop.Items;
        LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        Assert.True(state.Shop.TryGetRandomEx(7, 1, out var reason), reason);
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.NotSame(dictionary, state.Shop.Items);
        Assert.Same(old, dictionary[17]);
        Assert.NotSame(old, state.Shop.Items[17]);
        ItemIs(state.Shop.Items[17], 7, 17, 0);
        ItemIs(old, 7, 17, 11);
        Assert.Same(other, state.Shop.Items[19]);
        Assert.Equal(2, state.Shop.Items.Count);
    }

    [Fact]
    public void ZeroRandomCountAtomicallyRetiresValidTargetIdsAndPreservesUnrelatedStock()
    {
        using var state = new State();
        state.Metadata(true, 17, 17, 7);
        state.Metadata(true, 19, 19, 8);
        var removed = state.Record(17, 7, 17, 11);
        var other = state.Record(19, 8, 19, 17);
        var oldDictionary = state.Shop.Items;
        Assert.True(state.Shop.TryGetRandomEx(7, 0, out var reason), reason);
        Assert.NotSame(oldDictionary, state.Shop.Items);
        Assert.Same(removed, oldDictionary[17]);
        Assert.False(state.Shop.Items.ContainsKey(17));
        Assert.Same(other, Assert.Single(state.Shop.Items).Value);
        ItemIs(removed, 7, 17, 11);
    }

    [Fact]
    public void EmptyCandidateSelectionRefusesWithoutMutatingUnrelatedRows()
    {
        using var state = new State();
        var row = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryGetRandomEx(7, 1, out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Same(row, Assert.Single(state.Shop.Items).Value);
        ItemIs(row, 7, 17, 9);
    }

    [Fact]
    public void ResetPublishesEightGenuineDistinctShop168RowsWithoutMutatingOldBook()
    {
        using var state = new State();
        for (uint id = 1001; id <= 1008; id++) state.Metadata(true, id, id, 168);
        var old = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        Assert.True(state.Shop.TryReset(out var reason), reason);
        Assert.NotSame(dictionary, state.Shop.Items);
        Assert.Same(old, Assert.Single(dictionary).Value);
        Assert.Equal(Enumerable.Range(1001, 8).Select(id => (uint)id), state.Shop.Items.Keys.OrderBy(id => id));
        Assert.All(state.Shop.Items.Values, row => ItemIs(row, 168, row.goods_rec_id, 0));
        ItemIs(old, 7, 17, 9);
        Assert.False(state.Shop.Items.ContainsKey(17));
    }

    [Fact]
    public void LoadingGateReadsPhysicalGoods168EvenInForeignShopAndDoesNotUseShopId()
    {
        using var state = new State();
        var present = state.Record(168, 999, 168, 9);
        Assert.True(state.Shop.TryLoading(out var reason), reason);
        Assert.Same(present, Assert.Single(state.Shop.Items).Value);
        state.Shop.Items.Clear();
        var unrelated = state.Record(17, 168, 17, 11);
        Assert.False(state.Shop.TryLoading(out reason));
        Assert.Same(unrelated, Assert.Single(state.Shop.Items).Value);
        ItemIs(unrelated, 168, 17, 11);
    }

    [Fact]
    public void LoadingWithoutPhysicalGoods168RefreshesMatchingRowsOnEveryCallAndRetainsOthers()
    {
        using var state = new State();
        for (uint id = 1001; id <= 1008; id++) state.Metadata(true, id, id, 168);
        var unrelated = state.Record(99, 168, 99, 9);
        Assert.True(state.Shop.TryLoading(out var reason), reason);
        var first = state.Shop.Items.Where(pair => pair.Key != 99).ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Equal(8, first.Count);
        state.Shop.Items[1001].times = 3;
        Assert.True(state.Shop.TryLoading(out reason), reason);
        Assert.Equal(9, state.Shop.Items.Count);
        Assert.Same(unrelated, state.Shop.Items[99]);
        foreach (uint id in first.Keys)
        {
            Assert.NotSame(first[id], state.Shop.Items[id]);
            ItemIs(state.Shop.Items[id], 168, id, 0);
        }
        Assert.Equal(3u, first[1001].times);
    }

    [Fact]
    public void ResetWithEmptyMetadataPreservesTheCompleteOldBookAndReportsRefusal()
    {
        using var state = new State();
        var old = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryReset(out var reason));
        Assert.StartsWith("exchangeShop", reason);
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Same(old, Assert.Single(dictionary).Value);
        ItemIs(old, 7, 17, 9);
    }
}
