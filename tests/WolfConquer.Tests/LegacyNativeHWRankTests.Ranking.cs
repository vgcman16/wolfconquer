using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHWRankTests
{
    [Theory]
    [InlineData(420, 2)] [InlineData(430, 5)] [InlineData(440, 6)] [InlineData(450, 3)]
    [InlineData(460, 4)] [InlineData(480, 9)] [InlineData(481, 7)] [InlineData(490, 8)]
    [InlineData(410, 1)] [InlineData(0, 1)] [InlineData(65535, 1)]
    public void GetIndexRetainsEveryNativeArmAndDominatorFallback(ushort weapon, uint expected) =>
        Assert.Equal((Ranking.Type)expected, Ranking.GetIndex((LegacyNativeMagicType.WeaponsType)weapon));

    [Fact]
    public void CreateOverwritesTenRanksRetainsExtrasAndDoesNotTouchNinjaCatalog()
    {
        using var state = new State();
        var ninja = LegacyNativeNinjaRank.Ranks;
        Ranking.Create();
        var ranks = Ranking.Ranks;
        var first = ranks.GetValues();
        Assert.Equal(10, first.Length);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => (Ranking.Type)i), ranks.Keys);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Ranking.Type)));
        Assert.Equal(Enumerable.Range(0, 11).Select(i => (uint)i), Enum.GetValues<Ranking.Type>().Select(i => (uint)i));
        var extra = new Ranking.Rank((Ranking.Type)99) { MaxItems = 7 };
        ranks.Add((Ranking.Type)99, extra); first[0].UpdateItem(Entry());
        Ranking.Create();
        Assert.Same(ranks, Ranking.Ranks); Assert.Equal(11, ranks.Count);
        for (int i = 0; i < 10; i++)
        {
            var rank = ranks[(Ranking.Type)i];
            Assert.Equal((Ranking.Type)i, rank._Type); Assert.Equal(10, rank.MaxItems);
            Assert.NotSame(first[i], rank); Assert.NotSame(first[i].SynRoot, rank.SynRoot); Assert.Empty(rank.RankingList);
        }
        Assert.Single(first[0].RankingList); Assert.Same(extra, ranks[(Ranking.Type)99]);
        Assert.NotSame(first, ranks.GetValues()); Assert.Same(ninja, LegacyNativeNinjaRank.Ranks);
    }

    [Fact]
    public void UpdateSortsUnsignedScoresCapsTenAndKeepsEntriesByReference()
    {
        var rank = new Ranking.Rank(Ranking.Type.Faith) { MaxItems = 10 };
        for (uint id = 1; id <= 12; id++) rank.UpdateItem(Entry(id, id == 12 ? uint.MaxValue : id));
        Assert.Equal(new uint[] { 12, 11, 10, 9, 8, 7, 6, 5, 4, 3 }, rank.RankingList.Keys);
        var escaped = rank.RankingList;
        var replacement = Entry(5, uint.MaxValue);
        rank.UpdateItem(replacement);
        Assert.NotSame(escaped, rank.RankingList); Assert.Same(replacement, escaped[5]);
        Assert.Same(replacement, rank.RankingList[5]); Assert.Equal(new uint[] { 12, 5 }, rank.RankingList.Keys.Take(2));
        replacement.Name = "physical"; Assert.Equal("physical", rank.RankingList[5].Name);
        rank.Remove(12); Assert.Equal(5u, rank.RankingList.Keys.First()); Assert.Equal(9, rank.RankingList.Count);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void NonpositiveCapDropsRowOnlyAfterEscapedDictionaryMutation(int cap)
    {
        var rank = new Ranking.Rank(Ranking.Type.Main) { MaxItems = cap };
        var old = rank.RankingList; var row = Entry(); rank.UpdateItem(row);
        Assert.Empty(rank.RankingList); Assert.NotSame(old, rank.RankingList); Assert.Same(row, old[41]);
    }

    [Fact]
    public void RankFaultsRetainEarlierInsertOrRemoveAndDoNotPublishSortedReplacement()
    {
        var rank = new Ranking.Rank(Ranking.Type.Main) { MaxItems = 10 };
        var old = rank.RankingList; old.Add(90, null!); old.Add(91, Entry(91));
        rank.Remove(999); Assert.Same(old, rank.RankingList);
        var added = Entry();
        Assert.Throws<NullReferenceException>(() => rank.UpdateItem(added));
        Assert.Same(old, rank.RankingList); Assert.Same(added, old[41]);
        Assert.Throws<NullReferenceException>(() => rank.Remove(41));
        Assert.False(old.ContainsKey(41)); Assert.Same(old, rank.RankingList);
        Assert.Throws<NullReferenceException>(() => rank.UpdateItem(null!)); Assert.Equal(2, old.Count);
    }

    [Fact]
    public void QueriesUsePhysicalUidAndCurrentInsertionOrderRatherThanKeysOrScoreResort()
    {
        using var state = new State(); Ranking.Create();
        var rank = Ranking.Ranks[Ranking.Type.Judge]; var first = Entry(17, 1); var next = Entry(19, 100);
        rank.RankingList.Add(700, first); rank.RankingList.Add(701, next);
        Assert.Same(first, Ranking.BestOf(Ranking.Type.Judge)); Assert.Same(first, Ranking.GetInfo(Ranking.Type.Judge, 17));
        Assert.Equal(2u, Ranking.GetMyRank(Ranking.Type.Judge, 19));
        Assert.Null(Ranking.GetInfo(Ranking.Type.Judge, 700)); Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Judge, 700));
        first.UID = 23;
        Assert.Null(Ranking.GetInfo(Ranking.Type.Judge, 17)); Assert.Same(first, Ranking.GetInfo(Ranking.Type.Judge, 23));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Judge, 23)); Assert.Null(Ranking.BestOf(Ranking.Type.Main));
        Assert.Throws<NullReferenceException>(() => Ranking.BestOf(Ranking.Type.Count));
        rank.RankingList[702] = null!;
        Assert.Throws<NullReferenceException>(() => Ranking.GetInfo(Ranking.Type.Judge, 23)); // Count scans past the match.
    }

    [Fact]
    public void GlobalRemoveUsesLiveSafeDictionaryValuesNotItsRetainedCachedArray()
    {
        using var state = new State(); Ranking.Create();
        var old = Ranking.Ranks[Ranking.Type.Main]; old.UpdateItem(Entry());
        var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(Ranking.Type.Main) { MaxItems = 10 }; replacement.UpdateItem(Entry());
        Ranking.Ranks[Ranking.Type.Main] = replacement;
        Assert.Same(cached, Ranking.Ranks.GetValues()); Ranking.Remove(41);
        Assert.Empty(replacement.RankingList); Assert.Single(old.RankingList); Assert.Same(old, cached[0]);
    }
}
