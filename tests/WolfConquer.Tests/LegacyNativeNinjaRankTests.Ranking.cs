using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Fact]
    public void CreateOverwritesSixStandardBucketsRetainsExtrasAndInvalidatesRealCache()
    {
        using var state = new State();
        Ranking.Create();
        var ranks = Ranking.Ranks;
        var first = ranks.GetValues();
        Assert.Equal(6, first.Length);
        Assert.Equal(Enumerable.Range(0, 6).Select(value => (Ranking.Type)value), ranks.Keys);
        Assert.All(first, rank => { Assert.Equal(10, rank.MaxItems); Assert.Empty(rank.RankingList); });
        var extra = new Ranking.Rank((Ranking.Type)99) { MaxItems = 7 };
        ranks.Add((Ranking.Type)99, extra);
        first[0].UpdateItem(Entry());
        Ranking.Create();
        Assert.Same(ranks, Ranking.Ranks);
        Assert.Equal(7, ranks.Count);
        for (int index = 0; index < 6; index++)
        {
            var rank = ranks[(Ranking.Type)index];
            Assert.Equal((Ranking.Type)index, rank._Type);
            Assert.NotSame(first[index], rank);
            Assert.NotSame(first[index].SynRoot, rank.SynRoot);
            Assert.Equal(10, rank.MaxItems);
            Assert.Empty(rank.RankingList);
        }
        Assert.Single(first[0].RankingList);
        Assert.Same(extra, ranks[(Ranking.Type)99]);
        Assert.NotSame(first, ranks.GetValues());
    }

    [Fact]
    public void UpdateSortsUnsignedScoresCapsTenAndRetainsPhysicalEntryAliases()
    {
        var rank = new Ranking.Rank(Ranking.Type.Fire) { MaxItems = 10 };
        Ranking.Entry? largest = null;
        for (uint id = 1; id <= 12; id++)
        {
            var row = Entry(id, id == 12 ? uint.MaxValue : id);
            if (id == 12) largest = row;
            rank.UpdateItem(row);
        }
        Assert.Equal(new uint[] { 12, 11, 10, 9, 8, 7, 6, 5, 4, 3 }, rank.RankingList.Keys);
        Assert.Same(largest, rank.RankingList[12]);
        largest!.Name = "live";
        Assert.Equal("live", rank.RankingList[12].Name);
        var old = rank.RankingList;
        var replacement = Entry(5, uint.MaxValue);
        rank.UpdateItem(replacement);
        Assert.NotSame(old, rank.RankingList);
        Assert.Same(replacement, old[5]); // Mutation precedes sorted owner replacement.
        Assert.Same(replacement, rank.RankingList[5]);
        Assert.Equal(new uint[] { 12, 5 }, rank.RankingList.Keys.Take(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroAndNegativeMaxItemsDropNewRowAfterMutatingEscapedDictionary(int limit)
    {
        var rank = new Ranking.Rank(Ranking.Type.Fire) { MaxItems = limit };
        var old = rank.RankingList;
        var row = Entry();
        rank.UpdateItem(row);
        Assert.Empty(rank.RankingList);
        Assert.NotSame(old, rank.RankingList);
        Assert.Same(row, old[41]);
    }

    [Fact]
    public void RemoveMissingDoesNotSortWhilePresentRemovalPublishesBeforeLaterNullFault()
    {
        var rank = new Ranking.Rank(Ranking.Type.Overall) { MaxItems = 10 };
        var row = Entry();
        rank.RankingList.Add(41, row);
        rank.RankingList.Add(91, null!);
        rank.RankingList.Add(92, Entry(92));
        var old = rank.RankingList;
        rank.Remove(99);
        Assert.Same(old, rank.RankingList);
        Assert.Throws<NullReferenceException>(() => rank.Remove(41));
        Assert.Same(old, rank.RankingList);
        Assert.False(old.ContainsKey(41));
        Assert.Null(old[91]);
    }

    [Fact]
    public void UpdateSortFaultRetainsInsertedRowAndOriginalDictionary()
    {
        var rank = new Ranking.Rank(Ranking.Type.Overall) { MaxItems = 10 };
        rank.RankingList.Add(91, null!);
        var old = rank.RankingList;
        var row = Entry();
        Assert.Throws<NullReferenceException>(() => rank.UpdateItem(row));
        Assert.Same(old, rank.RankingList);
        Assert.Same(row, old[41]);
        Assert.Throws<NullReferenceException>(() => rank.UpdateItem(null!));
        Assert.Equal(2, old.Count);
    }

    [Fact]
    public void QueriesSearchPhysicalUidRatherThanDictionaryKeyAndDoNotResortLiveRows()
    {
        using var state = new State();
        Ranking.Create();
        var rank = Ranking.Ranks[Ranking.Type.Fire];
        var first = Entry(17, 1);
        var second = Entry(19, 100);
        rank.RankingList.Add(700, first);
        rank.RankingList.Add(701, second);
        Assert.Same(first, Ranking.BestOf(Ranking.Type.Fire));
        Assert.Same(first, Ranking.GetInfo(Ranking.Type.Fire, 17));
        Assert.Equal(2u, Ranking.GetMyRank(Ranking.Type.Fire, 19));
        Assert.Null(Ranking.GetInfo(Ranking.Type.Fire, 700));
        Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Fire, 700));
        first.UID = 23;
        Assert.Null(Ranking.GetInfo(Ranking.Type.Fire, 17));
        Assert.Same(first, Ranking.GetInfo(Ranking.Type.Fire, 23));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Fire, 23));
        Assert.Null(Ranking.BestOf(Ranking.Type.Water));
        Assert.Throws<NullReferenceException>(() => Ranking.BestOf(Ranking.Type.Count));
    }

    [Fact]
    public void GlobalRemoveUsesLiveValuesRatherThanStaleSafeDictionaryCache()
    {
        using var state = new State();
        Ranking.Create();
        var old = Ranking.Ranks[Ranking.Type.Overall];
        old.UpdateItem(Entry());
        var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(Ranking.Type.Overall) { MaxItems = 10 };
        replacement.UpdateItem(Entry());
        Ranking.Ranks[Ranking.Type.Overall] = replacement;
        Assert.Same(cached, Ranking.Ranks.GetValues());
        Ranking.Remove(41);
        Assert.Empty(replacement.RankingList);
        Assert.Single(old.RankingList);
        Assert.Same(old, cached[0]);
    }
}
