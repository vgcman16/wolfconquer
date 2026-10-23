using Ranking = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHWRankTests
{
    [Fact]
    public void AnyHundredWeaponsQueriesReturnEmptyResultsForMissingOrEmptyBuckets()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Judge);
        Ranking.Ranks.Add(rank._Type, rank);
        Assert.Null(Ranking.GetInfo(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17));
        Assert.Null(Ranking.GetInfo(Ranking.Type.Count, 17)); Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Count, 17));
        Assert.Empty(rank.RankingList);
    }

    [Fact]
    public void AnyHundredWeaponsQueriesKeepPhysicalUidOrderAndFollowRealEntryMutation()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Judge);
        Ranking.Ranks.Add(rank._Type, rank); var first = Entry(17, 1); var second = Entry(19, uint.MaxValue);
        rank.RankingList.Add(700, first); rank.RankingList.Add(701, second);
        Assert.Same(first, Ranking.GetInfo(rank._Type, 17)); Assert.Equal(2u, Ranking.GetMyRank(rank._Type, 19));
        Assert.Null(Ranking.GetInfo(rank._Type, 700)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 700));
        first.UID = 23; Assert.Null(Ranking.GetInfo(rank._Type, 17));
        Assert.Same(first, Ranking.GetInfo(rank._Type, 23)); Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 23));
        Assert.Equal(new uint[] { 700, 701 }, rank.RankingList.Keys);
    }

    [Fact]
    public void AnyHundredWeaponsQueriesSkipNullRowsWithoutMutatingOrCountingThem()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Judge);
        Ranking.Ranks.Add(rank._Type, rank); var row = Entry(17);
        rank.RankingList.Add(700, null!); rank.RankingList.Add(701, row); rank.RankingList.Add(702, null!);
        Assert.Same(row, Ranking.GetInfo(rank._Type, 17)); Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 17));
        Assert.Null(Ranking.GetInfo(rank._Type, 99)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 99));
        Assert.Equal(3, rank.RankingList.Count); Assert.Null(rank.RankingList[700]); Assert.Null(rank.RankingList[702]);
    }

    [Fact]
    public void AnyHundredWeaponsQueriesUseLiveRegistryAndListNotStaleSafeDictionaryCache()
    {
        using var state = new State(); var old = new Ranking.Rank(Ranking.Type.Judge);
        old.RankingList.Add(700, Entry(17)); Ranking.Ranks.Add(old._Type, old); var cache = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(old._Type); var row = Entry(19);
        replacement.RankingList.Add(701, row); Ranking.Ranks[old._Type] = replacement;
        Assert.Same(cache, Ranking.Ranks.GetValues()); Assert.Same(old, Assert.Single(cache));
        Assert.Same(row, Ranking.GetInfo(old._Type, 19)); Assert.Equal(1u, Ranking.GetMyRank(old._Type, 19));
        Assert.Null(Ranking.GetInfo(old._Type, 17)); var priorList = replacement.RankingList; replacement.RankingList = new();
        Assert.Null(Ranking.GetInfo(old._Type, 19)); Assert.Equal(0u, Ranking.GetMyRank(old._Type, 19));
        Assert.Same(row, priorList[701]); Assert.Single(old.RankingList);
    }
}
