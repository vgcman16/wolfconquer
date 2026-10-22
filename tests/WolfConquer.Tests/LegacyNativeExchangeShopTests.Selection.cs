using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public void GetRandomExUsesRealSharedRngAndReplacesOneEligibleRowWithZeroUses()
    {
        using var state = new State();
        state.Metadata(true, 17, 17, 7, 3);
        state.Metadata(true, 19, 19, 8, 3);
        var old = state.Record(17, 99, 777, 11);
        var other = state.Record(19, 8, 19, 13);
        var dictionary = state.Shop.Items;
        LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        state.Shop.GetRandomEx(7, 1); // Exactly one eligible ID; no rejection loop is possible.
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.Same(dictionary, state.Shop.Items);
        Assert.NotSame(old, dictionary[17]);
        ItemIs(dictionary[17], 7, 17, 0);
        ItemIs(old, 99, 777, 11);
        Assert.Same(other, dictionary[19]);
        Assert.Equal(2, dictionary.Count);
    }

    [Fact]
    public void ZeroRandomCountStillRemovesMatchingPhysicalMetadataIdsRatherThanTableKeys()
    {
        using var state = new State();
        state.Metadata(true, 17, 999, 7);
        state.Metadata(true, 19, 777, 8);
        var removed = state.Record(999, 8, 555, 11);
        var keyAlias = state.Record(17, 7, 17, 13);
        var other = state.Record(777, 8, 777, 17);
        state.Shop.GetRandomEx(7, 0); // Never attempts lookup through the deliberately aliased key.
        Assert.False(state.Shop.Items.ContainsKey(999));
        Assert.Same(keyAlias, state.Shop.Items[17]);
        Assert.Same(other, state.Shop.Items[777]);
        ItemIs(removed, 8, 555, 11);
        Assert.Equal(2, state.Shop.Items.Count);
    }

    [Fact]
    public void EmptyCandidateSelectionFailsImmediatelyWithoutMutatingUnrelatedRows()
    {
        using var state = new State();
        var row = state.Record(17, 7, 17, 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Shop.GetRandomEx(7, 1));
        Assert.Same(row, Assert.Single(state.Shop.Items).Value);
        ItemIs(row, 7, 17, 9);
    }

    [Fact]
    public void ResetClearsOriginalDictionaryThenSelectsEightGenuineDistinctShop168Rows()
    {
        using var state = new State();
        for (uint id = 1001; id <= 1008; id++) state.Metadata(true, id, id, 168);
        var old = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        state.Shop.Reset(); // Eight unique, present, zero-use-admissible IDs make the native loop satisfiable.
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Equal(Enumerable.Range(1001, 8).Select(id => (uint)id), dictionary.Keys.OrderBy(id => id));
        Assert.All(dictionary.Values, row => ItemIs(row, 168, row.goods_rec_id, 0));
        ItemIs(old, 7, 17, 9);
        Assert.False(dictionary.ContainsKey(17));
    }

    [Fact]
    public void LoadingGateReadsPhysicalGoods168EvenInForeignShopAndDoesNotUseDictionaryKey()
    {
        using var state = new State();
        var alias = state.Record(17, 999, 168, 9);
        state.Shop.Loading(); // Empty metadata would fault immediately if the wrong shop-based gate ran.
        Assert.Same(alias, Assert.Single(state.Shop.Items).Value);
        state.Shop.Items.Clear();
        var keyed168 = state.Record(168, 168, 17, 11);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Shop.Loading());
        Assert.Same(keyed168, Assert.Single(state.Shop.Items).Value);
        ItemIs(keyed168, 168, 17, 11);
    }

    [Fact]
    public void LoadingWithoutPhysicalGoods168RefreshesMatchingRowsOnEveryCallAndRetainsOthers()
    {
        using var state = new State();
        for (uint id = 1001; id <= 1008; id++) state.Metadata(true, id, id, 168);
        var unrelated = state.Record(99, 168, 99, 9);
        state.Shop.Loading();
        var first = state.Shop.Items.Where(pair => pair.Key != 99).ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Equal(8, first.Count);
        state.Shop.Items[1001].times = 3;
        state.Shop.Loading();
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
    public void ResetWithEmptyMetadataLeavesClearPublishedBeforeImmediateSelectionFault()
    {
        using var state = new State();
        var old = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Shop.Reset());
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Empty(dictionary);
        ItemIs(old, 7, 17, 9);
    }
}
