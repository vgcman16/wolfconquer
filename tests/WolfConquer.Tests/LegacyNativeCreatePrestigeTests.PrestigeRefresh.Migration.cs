using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Fact]
    public void PrestigeRefreshEligibleClassMigrationRemovesOldMembershipAndPreservesExistingAndNewTiePositions() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeRefreshClient(fixture);
            var oldClassRunner = PublishPrestigeRefreshRow(42, 40_000);
            var targetPeer = PublishPrestigeRefreshRow(77, 40_000, Ranking.Type.Warrior, 2000);
            var oldEntry = client.PrestrigeEntry!;
            var oldPoints = client.PrestigePoints;
            var oldPointValues = oldPoints.ToArray();
            var physicalEffects = client.PerfectionStatus;
            var prior = new PrestigeRefreshGraphImage();
            SavedField(client.Player, "_class", 2000u);
            SetPrestigeRefreshScore(client, 40_000);

            client.CreatePrestigePoints();

            AssertPrestigeRefreshClientCommitted(client, oldEntry, oldPoints, oldPointValues, physicalEffects, 40_000);
            var current = client.PrestrigeEntry!;
            Assert.Equal(Ranking.Type.Warrior, current.type);
            Assert.Equal(2000u, current.Class);
            Assert.Equal(new uint[] { 42 }, Ranking.Ranks[Ranking.Type.Trojan].Items.Keys);
            Assert.Same(oldClassRunner, Ranking.Ranks[Ranking.Type.Trojan].BestOfClass);
            Assert.Equal(new uint[] { 77, 41 }, Ranking.Ranks[Ranking.Type.Warrior].Items.Keys);
            Assert.Same(targetPeer, Ranking.Ranks[Ranking.Type.Warrior].BestOfClass);
            Assert.Equal(new uint[] { 41, 42, 77 }, Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Same(current, Ranking.BestOfTheWorld);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.Warrior].Items[41]);
            Assert.Same(current, Ranking.GetInfo(41));
            Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Trojan, 41));
            Assert.Equal(2u, Ranking.GetMyRank(Ranking.Type.Warrior, 41));
            Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.World, 41));
            Assert.All(Ranking.Ranks.Where(pair => pair.Key is not Ranking.Type.World and not Ranking.Type.Warrior),
                pair => Assert.False(pair.Value.Items.ContainsKey(41)));
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Theory]
    [InlineData(10000u)]
    [InlineData(10001u)]
    [InlineData(10002u)]
    public void PrestigeRefreshLiteralWorldOnlyMigrationRemovesEveryOldClassWithoutCreatingTheMissingTarget(uint characterClass) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeRefreshClient(fixture);
            var oldClassRunner = PublishPrestigeRefreshRow(42, 40_000);
            var oldEntry = client.PrestrigeEntry!;
            var oldPoints = client.PrestigePoints;
            var oldPointValues = oldPoints.ToArray();
            var physicalEffects = client.PerfectionStatus;
            SavedField(client.Player, "_class", characterClass);
            var targetCategory = Ranking.Type.WaterTao; // An unrelated owner need not exist for World-only.
            Ranking.Ranks.Remove(targetCategory);
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeRefreshGraphImage();
            SetPrestigeRefreshScore(client, 40_000);

            client.CreatePrestigePoints();

            AssertPrestigeRefreshClientCommitted(client, oldEntry, oldPoints, oldPointValues, physicalEffects, 40_000);
            Assert.False(Ranking.Ranks.ContainsKey(targetCategory));
            Assert.Equal(characterClass, client.PrestrigeEntry!.Class);
            Assert.Equal(Ranking.Type.World, client.PrestrigeEntry.type);
            Assert.Same(client.PrestrigeEntry, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.Same(client.PrestrigeEntry, Ranking.BestOfTheWorld);
            Assert.Same(client.PrestrigeEntry, Ranking.GetInfo(41));
            Assert.All(Ranking.Ranks.Where(pair => pair.Key != Ranking.Type.World),
                pair => Assert.False(pair.Value.Items.ContainsKey(41)));
            Assert.Same(oldClassRunner, Ranking.Ranks[Ranking.Type.Trojan].BestOfClass);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });
}
