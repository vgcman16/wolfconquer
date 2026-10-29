using System.Globalization;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void MissingLoadRetainsPriorConsumerQueriesAndEscapedGraphIdentity()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var priorRows = SeedLoadConsumerGraph();
        var prior = new LoadConsumerGraph();

        var error = Record.Exception(Ranking.Load);

        Assert.Same(prior.Graph, Ranking.Ranks);
        prior.AssertUnchanged();
        AssertPriorLoadConsumerQueries(priorRows);
        Assert.IsType<FileNotFoundException>(error);
    }

    [Fact]
    public void FailedLoadDoesNotPublishValidHighPrefixOrReplacePriorConsumerGraph()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var priorRows = SeedLoadConsumerGraph();
        var prior = new LoadConsumerGraph();
        files.Seed(Text(Row(91, 100_000), Row(92, type: "12")));

        var error = Record.Exception(Ranking.Load);

        Assert.Same(prior.Graph, Ranking.Ranks);
        prior.AssertUnchanged();
        AssertPriorLoadConsumerQueries(priorRows);
        Assert.Null(Ranking.GetInfo(91));
        Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.World, 91));
        Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Ninja, 91));
        Assert.IsType<FormatException>(error);
    }

    [Fact]
    public void SuccessfulLoadSwitchesConsumersToOneFreshGraphWithoutMutatingEscapedRows()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var priorRows = SeedLoadConsumerGraph();
        var prior = new LoadConsumerGraph();
        files.Seed(Text(
            LoadConsumerRow(71, 30_000, Ranking.Type.Ninja, "Replacement Same UID", 7),
            LoadConsumerRow(uint.MaxValue, 50_000, Ranking.Type.Warrior, "New Champion", 8, 9)));

        Ranking.Load();

        Assert.NotSame(prior.Graph, Ranking.Ranks);
        AssertFreshLoadConsumerBuckets(prior.Graph);
        prior.AssertUnchanged();
        var replacement = Ranking.Ranks[Ranking.Type.Ninja].Items[71];
        var champion = Ranking.Ranks[Ranking.Type.Warrior].Items[uint.MaxValue];
        Assert.NotSame(priorRows[0], replacement);
        Assert.Equal("Replacement Same UID", replacement.Name);
        Assert.Same(replacement, Ranking.GetInfo(71));
        Assert.Same(replacement, Ranking.Ranks[Ranking.Type.World].Items[71]);
        Assert.Same(champion, Ranking.GetInfo(uint.MaxValue));
        Assert.Same(champion, Ranking.Ranks[Ranking.Type.World].Items[uint.MaxValue]);
        Assert.Same(champion, Ranking.Ranks[Ranking.Type.Warrior].BestOfClass);
        Assert.Same(champion, Ranking.BestOfTheWorld);
        Assert.Equal(2u, Ranking.GetMyRank(Ranking.Type.World, 71));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Ninja, 71));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.World, uint.MaxValue));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Warrior, uint.MaxValue));
        foreach (uint removedUid in new uint[] { 72, 73 })
        {
            Assert.Null(Ranking.GetInfo(removedUid));
            Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.World, removedUid));
        }
    }

    [Fact]
    public void ValidEmptyLoadResetsAllConsumerQueriesAndLeavesEscapedGraphUntouched()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var priorRows = SeedLoadConsumerGraph();
        var prior = new LoadConsumerGraph();
        files.Seed(Text());

        Ranking.Load();

        Assert.NotSame(prior.Graph, Ranking.Ranks);
        AssertFreshLoadConsumerBuckets(prior.Graph);
        prior.AssertUnchanged();
        Assert.All(Ranking.Ranks.Values, rank =>
        {
            Assert.Empty(rank.Items);
            Assert.Null(rank.BestOfClass);
        });
        Assert.Null(Ranking.BestOfTheWorld);
        foreach (var oldRow in priorRows)
        {
            Assert.Null(Ranking.GetInfo(oldRow.UID));
            Assert.Equal(0u, Ranking.GetMyRank(oldRow.type, oldRow.UID));
            Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.World, oldRow.UID));
        }
    }

    [Theory]
    [InlineData(0, uint.MaxValue)]
    [InlineData(1, 2_147_483_648u)]
    [InlineData(2, 41u)]
    [InlineData(43, uint.MaxValue)]
    [InlineData(44, 2_147_483_648u)]
    public void FileLoadConsumersAcceptVariablePointCountsAndFullPositiveUInt32Uid(int pointCount, uint uid)
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        SeedLoadConsumerGraph();
        var prior = new LoadConsumerGraph();
        var points = Enumerable.Range(0, pointCount).Select(index => (uint)index).ToArray();
        files.Seed(Text(LoadConsumerRow(uid, 20_000, Ranking.Type.Ninja, "Variable Points", points)));

        Ranking.Load();

        Assert.NotSame(prior.Graph, Ranking.Ranks);
        AssertFreshLoadConsumerBuckets(prior.Graph);
        prior.AssertUnchanged();
        var loaded = Assert.Single(Ranking.Ranks[Ranking.Type.Ninja].Items).Value;
        Assert.Equal(uid, loaded.UID);
        Assert.Equal("Variable Points", loaded.Name);
        Assert.Equal(points, loaded.Points);
        Assert.Same(loaded, Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value);
        Assert.Same(loaded, Ranking.Ranks[Ranking.Type.Ninja].BestOfClass);
        Assert.Same(loaded, Ranking.BestOfTheWorld);
        Assert.Same(loaded, Ranking.GetInfo(uid));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Ninja, uid));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.World, uid));
    }

    private static Ranking.Entry[] SeedLoadConsumerGraph()
    {
        Ranking.Create();
        var champion = Entry(71, 90_000);
        champion.Name = "Prior Champion";
        var secondNinja = Entry(72, 40_000);
        secondNinja.Name = "Prior Ninja";
        var trojan = Entry(73, 50_000);
        trojan.Name = "Prior Trojan";
        trojan.type = Ranking.Type.Trojan;
        trojan.Class = 1000;
        var rows = new[] { champion, secondNinja, trojan };
        foreach (var row in rows)
        {
            Ranking.Ranks[row.type].AddItem(row);
            Ranking.Ranks[Ranking.Type.World].AddItem(row);
        }
        AssertPriorLoadConsumerQueries(rows);
        return rows;
    }

    private static void AssertPriorLoadConsumerQueries(IReadOnlyList<Ranking.Entry> rows)
    {
        Assert.Same(rows[0], Ranking.BestOfTheWorld);
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            Assert.Same(row, Ranking.GetInfo(row.UID));
            Assert.Same(row, Ranking.Ranks[row.type].Items[row.UID]);
            Assert.Same(row, Ranking.Ranks[Ranking.Type.World].Items[row.UID]);
            Assert.Equal(index == 1 ? 2u : 1u, Ranking.GetMyRank(row.type, row.UID));
            Assert.Equal(index switch { 0 => 1u, 1 => 3u, _ => 2u },
                Ranking.GetMyRank(Ranking.Type.World, row.UID));
        }
    }

    private static void AssertFreshLoadConsumerBuckets(
        LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> previous)
    {
        Assert.Equal(12, Ranking.Ranks.Count);
        Assert.Equal(Enumerable.Range(0, 12).Select(index => (Ranking.Type)index), Ranking.Ranks.Keys);
        foreach (var category in Ranking.Ranks.Keys)
        {
            var rank = Ranking.Ranks[category];
            Assert.NotSame(previous[category], rank);
            Assert.Equal(category, rank._Type);
            Assert.Equal(category == Ranking.Type.World ? 100 : 30, rank.MaxItems);
        }
    }

    private static string LoadConsumerRow(
        uint uid, uint totalPoints, Ranking.Type category, string name, params uint[] points)
    {
        var row = Row(uid, totalPoints, ((uint)category).ToString(CultureInfo.InvariantCulture), name, points.Length);
        return points.Length == 0
            ? row
            : row + string.Join('/', points.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private sealed class LoadConsumerGraph
    {
        internal LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> Graph { get; } = Ranking.Ranks;
        private readonly Ranking.Rank[] cache;
        private readonly long revision;
        private readonly (Ranking.Rank Rank, Dictionary<uint, Ranking.Entry> Items,
            KeyValuePair<uint, Ranking.Entry>[] Rows, Ranking.Entry? Best)[] buckets;
        private readonly (Ranking.Entry Row, object[] Values, uint[]? Points, uint[]? PointValues)[] rows;

        internal LoadConsumerGraph()
        {
            cache = Graph.GetValues();
            revision = Graph.Revision;
            buckets = cache.Select(rank => (rank, rank.Items, rank.Items.ToArray(), rank.BestOfClass)).ToArray();
            rows = cache.SelectMany(rank => rank.Items.Values).Distinct()
                .Select(row => (row, Values(row), row.Points, row.Points?.ToArray())).ToArray();
        }

        internal void AssertUnchanged()
        {
            Assert.Equal(12, Graph.Count);
            Assert.Equal(cache, Graph.GetValues());
            Assert.Equal(revision, Graph.Revision);
            foreach (var bucket in buckets)
            {
                Assert.Same(bucket.Rank, Graph[bucket.Rank._Type]);
                Assert.Same(bucket.Items, bucket.Rank.Items);
                Assert.Same(bucket.Best, bucket.Rank.BestOfClass);
                Assert.Equal(bucket.Rows.Select(pair => pair.Key), bucket.Rank.Items.Keys);
                foreach (var pair in bucket.Rows)
                    Assert.Same(pair.Value, bucket.Rank.Items[pair.Key]);
            }
            foreach (var saved in rows)
            {
                Assert.Equal(saved.Values, Values(saved.Row));
                Assert.Same(saved.Points, saved.Row.Points);
                Assert.Equal(saved.PointValues, saved.Row.Points);
            }
        }
    }
}
