using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCodeFactorCombatRankAnyTests
{
    private sealed class State : IDisposable
    {
        private readonly Dictionary<Ranking.Type, Ranking.Rank> prior = Ranking.Ranks;
        internal State() => Ranking.Ranks = new();
        public void Dispose() => Ranking.Ranks = prior;
    }

    [Fact]
    public void AnyCombatQueriesHandleEmptyAndMissingBucketsWithoutChangingOwners()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Overall_Warrior);
        Ranking.Ranks[rank._Type] = rank; var list = rank.RankingList;
        Assert.Null(Ranking.GetInfo(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17));
        Assert.Null(Ranking.GetInfo(Ranking.Type.ColdMoon, 17)); Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.ColdMoon, 17));
        Assert.Same(list, rank.RankingList); Assert.Empty(list);
    }

    [Fact]
    public void AnyCombatQueriesUsePhysicalUidsAndFirstCurrentValueWithoutSorting()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Overall_Warrior);
        Ranking.Ranks[rank._Type] = rank;
        var first = new Ranking.Entry { UID = 17, TotalPoints = 1 };
        var second = new Ranking.Entry { UID = 19, TotalPoints = uint.MaxValue };
        rank.RankingList[700] = first; rank.RankingList[701] = second;
        Assert.Same(first, Ranking.GetInfo(rank._Type, 17)); Assert.Equal(2u, Ranking.GetMyRank(rank._Type, 19));
        Assert.Null(Ranking.GetInfo(rank._Type, 700)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 700));
        first.UID = 23;
        Assert.Null(Ranking.GetInfo(rank._Type, 17)); Assert.Same(first, Ranking.GetInfo(rank._Type, 23));
        Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 23)); Assert.Equal(new uint[] { 700, 701 }, rank.RankingList.Keys);
    }

    [Fact]
    public void AnyCombatQueriesSkipInvalidRowsAndReadReplacementListsWithoutRepairingThem()
    {
        using var state = new State(); var rank = new Ranking.Rank(Ranking.Type.Overall_Warrior);
        Ranking.Ranks[rank._Type] = rank; var entry = new Ranking.Entry { UID = 17 };
        rank.RankingList[1] = null!; rank.RankingList[2] = entry; var old = rank.RankingList;
        Assert.Same(entry, Ranking.GetInfo(rank._Type, 17)); Assert.Equal(1u, Ranking.GetMyRank(rank._Type, 17));
        Assert.Null(old[1]); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 99));
        rank.RankingList = new(); Assert.Null(Ranking.GetInfo(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17));
        Assert.Same(entry, old[2]); rank.RankingList = null!;
        Assert.Null(Ranking.GetInfo(rank._Type, 17)); Assert.Equal(0u, Ranking.GetMyRank(rank._Type, 17));
    }
}
