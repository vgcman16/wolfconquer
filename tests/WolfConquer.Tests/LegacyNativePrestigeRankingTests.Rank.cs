using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void CreateOverwritesTwelveNativeBucketsWithHundredWorldAndThirtyClassLimits()
    {
        using var state = new State();
        Ranking.Create();
        var first = Ranking.Ranks.GetValues();
        Assert.Equal(12, first.Length);
        Assert.Equal(Enumerable.Range(0,12).Select(i => (Ranking.Type)i), Ranking.Ranks.Keys);
        Assert.All(first, rank => Assert.Equal(rank._Type == Ranking.Type.World ? 100 : 30, rank.MaxItems));
        first[0].Items.Add(41, Entry());
        var extra = new Ranking.Rank((Ranking.Type)99);
        Ranking.Ranks.Add((Ranking.Type)99, extra);
        Ranking.Create();
        Assert.Equal(13, Ranking.Ranks.Count);
        Assert.Same(extra, Ranking.Ranks[(Ranking.Type)99]);
        Assert.NotSame(first, Ranking.Ranks.GetValues());
        Assert.All(Enumerable.Range(0,12), index => Assert.NotSame(first[index], Ranking.Ranks[(Ranking.Type)index]));
        Assert.Single(first[0].Items);
        Assert.Null(Ranking.Ranks[Ranking.Type.World].BestOfClass);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(19999u)]
    public void BelowTwentyThousandRemovesTheSameUidAndClearsTheLastChampionWithoutMutatingEscapedItems(uint points)
    {
        var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 1 };
        var prior = Entry();
        rank.AddItem(prior);
        var escaped = rank.Items;
        rank.AddItem(Entry(41, points));
        Assert.Empty(rank.Items);
        Assert.Null(rank.BestOfClass);
        Assert.NotSame(escaped, rank.Items);
        Assert.Same(prior, Assert.Single(escaped).Value);
        var empty = rank.Items;
        rank.AddItem(Entry(99, points));
        Assert.Same(empty, rank.Items); // A genuinely absent low UID remains a no-op.
        Assert.Throws<ArgumentNullException>(() => rank.AddItem(null!));
        Assert.Same(empty, rank.Items);
        Assert.Null(rank.BestOfClass);
    }

    [Fact]
    public void ExactThresholdAndUnsignedScoresSortCapAndKeepRealEntryAliases()
    {
        var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
        var rows = new[] { Entry(1,20000), Entry(2,20001), Entry(3,uint.MaxValue), Entry(4,20000) };
        foreach (var row in rows) rank.AddItem(row);
        Assert.Equal(new uint[] {3,2,1}, rank.Items.Keys);
        Assert.Same(rows[2], rank.BestOfClass);
        Assert.Same(rows[0], rank.Items[1]);
        var escaped = rank.Items;
        var replacement = Entry(1,uint.MaxValue);
        rank.AddItem(replacement);
        Assert.Same(rows[0], escaped[1]);
        Assert.Equal(20_000u, rows[0].TotalPoints);
        Assert.NotSame(escaped, rank.Items);
        Assert.Equal(new uint[] {3,1,2}, rank.Items.Keys);
        replacement.Name = "live";
        Assert.Equal("live", rank.Items[1].Name);
    }

    [Fact]
    public void RemoveMissingIsAnExactNoOpAndPresentRemovalRefreshesNextThenNullChampion()
    {
        var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
        var best = Entry(41,40000);
        var second = Entry(42,30000);
        rank.AddItem(best);
        rank.AddItem(second);
        var escaped = rank.Items;
        rank.Remove(99);
        Assert.Same(escaped, rank.Items);
        rank.Remove(41);
        Assert.NotSame(escaped, rank.Items);
        Assert.Same(best, escaped[41]);
        Assert.Same(second, rank.BestOfClass);
        Assert.Same(second, Assert.Single(rank.Items).Value);
        var secondEscaped = rank.Items;
        rank.Remove(42);
        Assert.Empty(rank.Items);
        Assert.Null(rank.BestOfClass);
        Assert.Same(second, Assert.Single(secondEscaped).Value);
    }

    [Fact]
    public void RemoveMalformedTailRefusesWithoutDeletionDictionaryReplacementOrChampionMutation()
    {
        var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
        var best = Entry();
        rank.AddItem(best);
        rank.Items.Add(77, null!);
        var escaped = rank.Items;
        Assert.Throws<InvalidOperationException>(() => rank.Remove(41));
        Assert.Same(escaped, rank.Items);
        Assert.Same(best, escaped[41]);
        Assert.Same(best, rank.BestOfClass);
        Assert.Null(escaped[77]);
    }

    [Fact]
    public void AddMalformedTailRefusesWithoutInsertingTheCandidateOrSelectingAPrefixChampion()
    {
        var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
        var first = Entry(1,20000);
        rank.Items.Add(1, first);
        rank.Items.Add(2, null!);
        var sentinel = Entry(99, 60_000);
        rank.BestOfClass = sentinel;
        var escaped = rank.Items;
        var inserted = Entry(3,50000);
        Assert.Throws<InvalidOperationException>(() => rank.AddItem(inserted));
        Assert.Same(escaped, rank.Items);
        Assert.Equal(new uint[] { 1, 2 }, escaped.Keys);
        Assert.Same(first, escaped[1]);
        Assert.Null(escaped[2]);
        Assert.Same(sentinel, rank.BestOfClass);
    }
}
