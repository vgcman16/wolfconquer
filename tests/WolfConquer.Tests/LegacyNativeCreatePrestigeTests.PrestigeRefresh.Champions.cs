using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(19_999u, false)]
    [InlineData(0u, true)]
    [InlineData(19_999u, true)]
    public void PrestigeRefreshHistoricalChampionOnlyMembershipSelectsActualSurvivorsOrNull(uint total, bool hasSurvivor) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeRefreshClient(fixture);
            var survivor = hasSurvivor ? PublishPrestigeRefreshRow(42, 40_000) : null;
            var oldEntry = client.PrestrigeEntry!;
            var oldPoints = client.PrestigePoints;
            var oldPointValues = oldPoints.ToArray();
            var physicalEffects = client.PerfectionStatus;
            // Reproduce only the historical raw-delete output in owned memory.
            // Cached champion identity still exposes UID41 after Items lost it.
            foreach (var category in new[] { Ranking.Type.World, Ranking.Type.Trojan })
            {
                Assert.True(Ranking.Ranks[category].Items.Remove(41));
                Assert.Same(oldEntry, Ranking.Ranks[category].BestOfClass);
            }
            var prior = new PrestigeRefreshGraphImage();
            SetPrestigeRefreshScore(client, total);

            client.CreatePrestigePoints();

            AssertPrestigeRefreshClientCommitted(client, oldEntry, oldPoints, oldPointValues, physicalEffects, total);
            Assert.Same(survivor, Ranking.BestOfTheWorld);
            Assert.Same(survivor, Ranking.Ranks[Ranking.Type.Trojan].BestOfClass);
            Assert.Null(Ranking.GetInfo(41));
            Assert.Equal(hasSurvivor ? new uint[] { 42 } : [], Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Equal(hasSurvivor ? new uint[] { 42 } : [], Ranking.Ranks[Ranking.Type.Trojan].Items.Keys);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void PrestigeRefreshNewAbsentLowUidDoesNotHealAnUnrelatedHistoricalChampion() =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(41, 50_000);
            foreach (var category in new[] { Ranking.Type.World, Ranking.Type.Trojan })
                Assert.True(Ranking.Ranks[category].Items.Remove(41));
            var prior = new PrestigeRefreshGraphImage();

            PublishPrestigeRefreshRow(99, 19_999);

            prior.RequireUnchanged();
        });
}
