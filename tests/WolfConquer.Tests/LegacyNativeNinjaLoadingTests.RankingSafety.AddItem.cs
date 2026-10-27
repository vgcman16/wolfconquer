using WolfConquer.Core.Data;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task AddItemCommitsRealCandidateAndEveryRankBeforeItsExactNotificationAttempt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        var previous = scope.Item(101, 101, 1);
        scope.Meta(1, 101, 1); scope.Meta(7, 100, 1, score: 1000);
        var escapedItems = scope.Ninja.Items;
        var owners = RankingCategories.Select(scope.Rank).ToArray();
        var escapedMaps = owners.Select(rank => rank.RankingList).ToArray();
        var former = escapedMaps.Select(map => map[17]).ToArray();

        scope.Ninja.AddItem(100, 1, 2, 3, 4);

        Assert.NotSame(escapedItems, scope.Ninja.Items);
        Assert.Same(previous, Assert.Single(escapedItems).Value);
        Assert.Same(previous, scope.Ninja.Items[101]);
        var added = scope.Ninja.Items[100];
        Assert.Equal(new uint[] { 100, 1, 2, 3, 4 }, new[] { added.ItemID, added.Level, added.Position, added.Exp, added.UK });
        for (var index = 0; index < RankingCategories.Length; index++)
        {
            var owner = scope.Rank(RankingCategories[index]);
            Assert.Same(owners[index], owner);
            Assert.NotSame(escapedMaps[index], owner.RankingList);
            Assert.Same(former[index], escapedMaps[index][17]);
            Assert.Equal(2000u, former[index].TotalPoints);
            Assert.Same(escapedMaps[index][18], owner.RankingList[18]);
            if (RankingCategories[index] is RankType.Overall or RankType.Fire)
            {
                Assert.Equal(1000u, owner.RankingList[17].TotalPoints);
                Assert.Equal(RankingCategories[index], owner.RankingList[17].Type);
            }
            else Assert.False(owner.RankingList.ContainsKey(17));
        }
        var expected = Convert.FromHexString("14005909080210111A0A08641001180220032800");
        scope.Sent(expected); scope.Serialized(0, expected);
        Assert.Equal(11, scope.Available);
    });

    [Theory]
    [InlineData("scoreOverflow")]
    [InlineData("levelWidth")]
    [InlineData("name")]
    public Task AddItemDerivedOrAppearanceRefusalKeepsPriorItemsRanksAndOutputs(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        var previous = scope.Item(101, 101, 1);
        var items = scope.Ninja.Items;
        scope.Meta(1, 101, 1); scope.Meta(7, 100, 1, score: 1000);
        switch (fault)
        {
            case "scoreOverflow": scope.Metadata[7].Score = uint.MaxValue; scope.Ninja.FireMastery = 1; break;
            case "levelWidth": scope.SetLevel(300); break;
            case "name": scope.Client.Player.Name = "bad/name"; break;
        }
        var before = new RankingGraphWitness();

        Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddItem(100, 1, 2, 3, 4));

        Assert.Same(items, scope.Ninja.Items);
        Assert.Same(previous, Assert.Single(items).Value);
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });
}
