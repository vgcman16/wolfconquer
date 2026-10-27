using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Fact]
    public void MissingWorldOwnerRejectsBeforeEitherRankPublishesTheNewPrestigeEntry()
    {
        using var fixture = new Fixture();
        ConfigureRankPublicationClient(fixture.Client, 1000);
        SeedRankPublicationPair(17, 30_000);
        Ranking.Ranks.Remove(Ranking.Type.World);
        var prior = new RankPublicationGraph();

        var error = Record.Exception(fixture.Client.CreatePrestigePoints);

        prior.AssertUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    }

    [Theory]
    [InlineData("null-tail")]
    [InlineData("mismatched-uid-tail")]
    [InlineData("null-items")]
    public void InvalidWorldStateRejectsWithoutPublishingAClassPrefix(string fault)
    {
        using var fixture = new Fixture();
        ConfigureRankPublicationClient(fixture.Client, 1000);
        SeedRankPublicationPair(17, 30_000);
        var world = Ranking.Ranks[Ranking.Type.World];
        world.MaxItems = 1;
        if (fault == "null-tail")
            world.Items.Add(999, null!);
        else if (fault == "mismatched-uid-tail")
            world.Items.Add(999, RankPublicationRow(23, 1)); // Invalid even outside the retained cap.
        else
            world.Items = null!;
        var prior = new RankPublicationGraph();

        var error = Record.Exception(fixture.Client.CreatePrestigePoints);

        prior.AssertUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void PreparedPairRetainsStableReplacementCapsAndSameEntryWithoutMutatingEscapedDictionaries()
    {
        using var fixture = new Fixture();
        ConfigureRankPublicationClient(fixture.Client, 1000);
        var replaced = SeedRankPublicationPair(41, 30_000);
        SeedRankPublicationPair(17, 20_000);
        var champion = SeedRankPublicationPair(19, 40_000);
        var classRank = Ranking.Ranks[Ranking.Type.Trojan];
        var world = Ranking.Ranks[Ranking.Type.World];
        classRank.MaxItems = world.MaxItems = 2;
        var oldClassItems = classRank.Items;
        var oldWorldItems = world.Items;

        fixture.Client.CreatePrestigePoints();

        var published = fixture.Client.PrestrigeEntry!;
        Assert.Equal(20_000u, published.TotalPoints);
        Assert.Equal(new uint[] { 19, 41 }, classRank.Items.Keys);
        Assert.Equal(new uint[] { 19, 41 }, world.Items.Keys);
        Assert.Same(published, classRank.Items[41]);
        Assert.Same(published, world.Items[41]);
        Assert.Same(champion, classRank.BestOfClass);
        Assert.Same(champion, world.BestOfClass);
        Assert.NotSame(oldClassItems, classRank.Items);
        Assert.NotSame(oldWorldItems, world.Items);
        Assert.Equal(new uint[] { 19, 41, 17 }, oldClassItems.Keys);
        Assert.Equal(new uint[] { 19, 41, 17 }, oldWorldItems.Keys);
        Assert.Same(replaced, oldClassItems[41]);
        Assert.Same(replaced, oldWorldItems[41]);
        Assert.Equal(30_000u, replaced.TotalPoints);
    }

    [Theory]
    [InlineData(10000u)]
    [InlineData(10001u)]
    [InlineData(10002u)]
    public void ExcludedClassesPublishOnlyWorldWithoutRequiringAnUnrelatedClassBucket(uint characterClass)
    {
        using var fixture = new Fixture();
        ConfigureRankPublicationClient(fixture.Client, characterClass);
        var category = Ranking.GetIndex(characterClass);
        Ranking.Ranks.Remove(category);

        fixture.Client.CreatePrestigePoints();

        Assert.False(Ranking.Ranks.ContainsKey(category));
        var published = fixture.Client.PrestrigeEntry!;
        Assert.Same(published, Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value);
        Assert.Same(published, Ranking.BestOfTheWorld);
    }

    [Fact]
    public void BelowThresholdNewEntryLeavesBothValidRankOwnersUnchanged()
    {
        using var fixture = new Fixture();
        ConfigureRankPublicationClient(fixture.Client, 1000);
        fixture.Client.Equipment!.CurentEquip = [];
        SeedRankPublicationPair(17, 30_000);
        var prior = new RankPublicationGraph();

        fixture.Client.CreatePrestigePoints();

        prior.AssertUnchanged();
        Assert.False(Ranking.Ranks[Ranking.Type.Trojan].Items.ContainsKey(41));
        Assert.False(Ranking.Ranks[Ranking.Type.World].Items.ContainsKey(41));
    }

    private static void ConfigureRankPublicationClient(LegacyNativeGameClientRuntime client, uint characterClass)
    {
        client.Player.ServerID = 2;
        SavedField(client.Player, "_class", characterClass);
        client.Equipment!.CurentEquip = [new() { ItemPoints = 10_000 }];
    }

    private static Ranking.Entry SeedRankPublicationPair(uint uid, uint totalPoints)
    {
        var entry = RankPublicationRow(uid, totalPoints);
        Ranking.Ranks[Ranking.Type.Trojan].AddItem(entry);
        Ranking.Ranks[Ranking.Type.World].AddItem(entry);
        return entry;
    }

    private static Ranking.Entry RankPublicationRow(uint uid, uint totalPoints) => new()
    {
        UID = uid,
        Name = "Prior " + uid,
        type = Ranking.Type.Trojan,
        Class = 1000,
        TotalPoints = totalPoints,
        Points = [7, 8],
    };

    private sealed class RankPublicationGraph
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly (Ranking.Type Key, Ranking.Rank Rank, Dictionary<uint, Ranking.Entry>? Items,
            KeyValuePair<uint, Ranking.Entry>[] Rows, Ranking.Entry? Champion)[] ranks;
        private readonly (Ranking.Entry Row, uint Uid, uint Total, string Name, Ranking.Type Category,
            uint CharacterClass, uint[]? Points, uint[]? PointValues)[] rows;

        internal RankPublicationGraph()
        {
            ranks = graph.Select(pair => (pair.Key, pair.Value, pair.Value.Items,
                pair.Value.Items?.ToArray() ?? [], pair.Value.BestOfClass)).ToArray();
            rows = ranks.SelectMany(rank => rank.Rows).Select(pair => pair.Value)
                .Where(row => row is not null).Distinct()
                .Select(row => (row, row.UID, row.TotalPoints, row.Name, row.type,
                    row.Class, row.Points, row.Points?.ToArray())).ToArray();
        }

        internal void AssertUnchanged()
        {
            Assert.Same(graph, Ranking.Ranks);
            Assert.Equal(ranks.Select(rank => rank.Key), graph.Keys);
            foreach (var saved in ranks)
            {
                Assert.Same(saved.Rank, graph[saved.Key]);
                Assert.Same(saved.Items, saved.Rank.Items);
                Assert.Same(saved.Champion, saved.Rank.BestOfClass);
                if (saved.Items is null)
                    Assert.Null(saved.Rank.Items);
                else
                {
                    Assert.Equal(saved.Rows.Select(pair => pair.Key), saved.Rank.Items.Keys);
                    foreach (var pair in saved.Rows)
                        Assert.Same(pair.Value, saved.Rank.Items[pair.Key]);
                }
            }
            foreach (var saved in rows)
            {
                Assert.Equal(saved.Uid, saved.Row.UID);
                Assert.Equal(saved.Total, saved.Row.TotalPoints);
                Assert.Equal(saved.Name, saved.Row.Name);
                Assert.Equal(saved.Category, saved.Row.type);
                Assert.Equal(saved.CharacterClass, saved.Row.Class);
                Assert.Same(saved.Points, saved.Row.Points);
                Assert.Equal(saved.PointValues, saved.Row.Points);
            }
        }
    }
}
