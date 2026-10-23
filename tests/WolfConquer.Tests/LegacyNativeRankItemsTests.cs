using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Rank = WolfConquer.Core.Data.LegacyNativeRankItems.Rank;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRankItemsTests
{
    [Fact]
    public void EmptyRankHasIndependentStorageAndMissingUidIsNativeOneHundred()
    {
        var rank = new Rank();
        var other = new Rank();
        Assert.Empty(rank.Items);
        Assert.Empty(rank.GetRank50Items());
        Assert.NotSame(rank.Items, other.Items);
        Assert.NotSame(rank.SynRoot, other.SynRoot);
        Assert.NotSame(rank.GetRank50Items(), other.GetRank50Items());
        Assert.Equal(100u, rank.GetItemRank(17));
        Assert.Throws<NullReferenceException>(() => rank.AddItem(null!));
        Assert.Empty(rank.Items);
    }

    [Fact]
    public void NewItemsSortByScoreAndPublishActualReferencesWithOneBasedRanks()
    {
        var rank = new Rank();
        var low = Item(1, 10);
        var high = Item(2, 30);
        var middle = Item(3, 20);
        rank.AddItem(low); rank.AddItem(high); rank.AddItem(middle);
        Assert.Equal(new[] { high, middle, low }, rank.GetRank50Items());
        Assert.Equal(new ushort[] { 1, 2, 3 }, rank.GetRank50Items().Select(item => item.PerfectionRank));
        Assert.Same(high, rank.Items[2]);
        Assert.Equal(3u, rank.GetItemRank(1));
        var alias = rank.GetRank50Items();
        Assert.Same(alias, rank.GetRank50Items());
        alias[0] = low;
        Assert.Same(low, rank.GetRank50Items()[0]); // Source exposes the array itself.
        Assert.Same(high, rank.Items[2]);
    }

    [Fact]
    public void BelowCapacityExistingItemComparesOldPositionToNewScoreAndCanRetainStaleTopArray()
    {
        var rank = new Rank();
        var old = Item(1, 10); old.Position = 77;
        rank.AddItem(old);
        var top = rank.GetRank50Items();
        var replacement = Item(1, 77); replacement.PerfectionRank = 99;
        rank.AddItem(replacement);
        Assert.Same(replacement, rank.Items[1]);
        Assert.Equal((ushort)1, replacement.PerfectionRank);
        Assert.Same(top, rank.GetRank50Items());
        Assert.Same(old, top[0]); // Not an old-ItemPoints comparison and not repaired.
    }

    [Fact]
    public void BelowCapacityDifferentOldPositionForcesFullSortEvenForSameScore()
    {
        var rank = new Rank();
        var old = Item(1, 10); old.Position = 9;
        var high = Item(2, 20);
        rank.AddItem(old); rank.AddItem(high);
        var top = rank.GetRank50Items();
        var replacement = Item(1, 30);
        rank.AddItem(replacement);
        Assert.NotSame(top, rank.GetRank50Items());
        Assert.Equal(new[] { replacement, high }, rank.GetRank50Items());
        Assert.Equal((ushort)1, replacement.PerfectionRank);
        Assert.Equal((ushort)2, high.PerfectionRank);
    }

    [Fact]
    public void FullRankExistingUidReplacementNeverSortsOrReplacesTopArray()
    {
        var rank = Full();
        var top = rank.GetRank50Items();
        var old = rank.Items[1];
        var replacement = Item(1, uint.MaxValue);
        rank.AddItem(replacement);
        Assert.Same(replacement, rank.Items[1]);
        Assert.Equal((ushort)50, replacement.PerfectionRank);
        Assert.Equal(50u, rank.GetItemRank(1));
        Assert.Same(top, rank.GetRank50Items());
        Assert.Same(old, top[^1]);
        Assert.DoesNotContain(replacement, top);
    }

    [Theory]
    [InlineData(0u)] [InlineData(1u)]
    public void FullRankRejectsNewScoresAtOrBelowTheActualLastArrayElement(uint score)
    {
        var rank = Full();
        var top = rank.GetRank50Items();
        var candidate = Item(99, score); candidate.PerfectionRank = 37;
        rank.AddItem(candidate);
        Assert.False(rank.Items.ContainsKey(99));
        Assert.Equal((ushort)37, candidate.PerfectionRank);
        Assert.Same(top, rank.GetRank50Items());
        Assert.Equal(50, rank.Items.Count);
    }

    [Fact]
    public void FullRankEvictsLowestWithoutClearingItsRankAndResortsEverySurvivingReference()
    {
        var rank = Full();
        var top = rank.GetRank50Items();
        var old = rank.Items[1];
        var candidate = Item(99, 100);
        rank.AddItem(candidate);
        Assert.False(rank.Items.ContainsKey(1));
        Assert.Equal(100u, rank.GetItemRank(1));
        Assert.Equal((ushort)50, old.PerfectionRank);
        Assert.Equal(50, rank.Items.Count);
        Assert.NotSame(top, rank.GetRank50Items());
        Assert.Same(candidate, rank.GetRank50Items()[0]);
        Assert.Equal(Enumerable.Range(1, 50).Select(value => (ushort)value),
            rank.GetRank50Items().Select(item => item.PerfectionRank));
    }

    [Fact]
    public void CreateAddsExactlyElevenRanksWithoutClearingOrReplacingTheDictionary()
    {
        var prior = LegacyNativeRankItems.RankPoll;
        try
        {
            LegacyNativeRankItems.RankPoll = new();
            var poll = LegacyNativeRankItems.RankPoll;
            LegacyNativeRankItems.CreateRanks();
            Assert.Same(poll, LegacyNativeRankItems.RankPoll);
            Assert.Equal(Enumerable.Range(1, 11).Select(value => (uint)value), poll.Keys);
            Assert.Equal(11, poll.Values.Distinct().Count());
            Assert.All(poll.Values, rank => Assert.Empty(rank.Items));
            Assert.Throws<ArgumentException>(() => LegacyNativeRankItems.CreateRanks());
            Assert.Equal(11, poll.Count);
        }
        finally { LegacyNativeRankItems.RankPoll = prior; }
    }

    private static Rank Full()
    {
        var rank = new Rank();
        for (uint value = 1; value <= 50; value++) rank.AddItem(Item(value, value));
        return rank;
    }

    private static LegacyNativeGameItemRuntime Item(uint uid, uint points) =>
        new() { Uid = uid, ITEM_ID = 111003, ItemPoints = points };
}
