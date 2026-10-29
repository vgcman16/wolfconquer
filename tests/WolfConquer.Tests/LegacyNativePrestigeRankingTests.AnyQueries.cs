using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void AnyPrestigeRankReturnsZeroForMissingEmptyOrUnavailableBuckets()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Ninja);
        Ranking.Ranks.Add(rank._Type, rank);
        Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Count, 17));
        rank.Items = null!; Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17)); Assert.Null(rank.BestOfClass);
    }

    [Fact]
    public void AnyPrestigeRankReadsPhysicalUidInValueOrderWithoutSortingOrBestOfChanges()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Ninja);
        Ranking.Ranks.Add(rank._Type, rank); var first = Entry(17, 20000); var second = Entry(19, uint.MaxValue);
        rank.Items.Add(700, first); rank.Items.Add(701, second); rank.BestOfClass = second;
        Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 17)); Assert.Equal(2u, Ranking.GetMyRank(rank._Type, 19));
        Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 700)); first.UID = 23;
        Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17)); Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 23));
        Assert.Same(second, rank.BestOfClass); Assert.Equal(new uint[] { 700, 701 }, rank.Items.Keys);
    }

    [Fact]
    public void AnyPrestigeRankSkipsNullRowsRatherThanCrashingOrRepairingStorage()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Ninja);
        Ranking.Ranks.Add(rank._Type, rank); var row = Entry(17);
        rank.Items.Add(700, null!); rank.Items.Add(701, row); rank.Items.Add(702, null!);
        Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 99));
        Assert.Equal(3, rank.Items.Count); Assert.Null(rank.Items[700]); Assert.Null(rank.Items[702]);
    }

    [Fact]
    public void AnyPrestigeRankReadsLiveRegistryAndCurrentItemsRatherThanCachedAliases()
    {
        using var state = new State(); var old = new Ranking.Rank(Ranking.Type.Ninja);
        old.Items.Add(700, Entry(17)); Ranking.Ranks.Add(old._Type, old); var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(old._Type); var row = Entry(19);
        replacement.Items.Add(701, row); Ranking.Ranks[old._Type] = replacement;
        long revision = Ranking.Ranks.Revision;
        Assert.Same(replacement, Assert.Single(Ranking.Ranks.GetValues())); Assert.Same(old, Assert.Single(cached));
        Assert.Equal(0u, Ranking.GetMyRank(old._Type, 17)); Assert.Equal(1u, Ranking.GetMyRank(old._Type, 19));
        var before = replacement.Items; replacement.Items = new();
        Assert.Equal(0u, Ranking.GetMyRank(old._Type, 19)); Assert.Same(row, before[701]); Assert.Equal(revision, Ranking.Ranks.Revision);
    }
}
