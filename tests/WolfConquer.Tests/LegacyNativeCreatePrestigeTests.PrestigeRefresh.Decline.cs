using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(19_999u, false)]
    [InlineData(0u, true)]
    [InlineData(19_999u, true)]
    public void PrestigeRefreshDeclineRemovesTheOldUidAndSelectsTheNextOrNullChampion(uint total, bool hasRunnerUp) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeRefreshClient(fixture);
            var runner = hasRunnerUp ? PublishPrestigeRefreshRow(42, 40_000) : null;
            var oldEntry = client.PrestrigeEntry!;
            var oldPoints = client.PrestigePoints;
            var oldPointValues = oldPoints.ToArray();
            var physicalEffects = client.PerfectionStatus;
            var prior = new PrestigeRefreshGraphImage();
            SetPrestigeRefreshScore(client, total);

            client.CreatePrestigePoints();

            AssertPrestigeRefreshClientCommitted(client, oldEntry, oldPoints, oldPointValues, physicalEffects, total);
            Assert.Equal(total == 19_999 ? 1_935u : 0u, client.PrestigePoints[4]);
            Assert.Equal(total == 19_999 ? 18_064u : 0u, client.PrestigePoints[3]);
            Assert.All(Ranking.Ranks.Values, rank => Assert.False(rank.Items.ContainsKey(41)));
            Assert.Null(Ranking.GetInfo(41));
            foreach (var category in Ranking.Ranks.Keys) Assert.Equal(0u, Ranking.GetMyRank(category, 41));
            Assert.Same(runner, Ranking.BestOfTheWorld);
            Assert.Same(runner, Ranking.Ranks[Ranking.Type.Trojan].BestOfClass);
            Assert.Equal(hasRunnerUp ? new uint[] { 42 } : [], Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Equal(hasRunnerUp ? new uint[] { 42 } : [], Ranking.Ranks[Ranking.Type.Trojan].Items.Keys);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Theory]
    [InlineData(0u, false)]
    [InlineData(19_999u, false)]
    [InlineData(0u, true)]
    [InlineData(19_999u, true)]
    public void PrestigeRefreshAbsentLowUidIsAnExactNoOpAfterValidFullOrSparseAdmission(uint total, bool worldOnly) =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(17, 30_000, includeClass: !worldOnly);
            if (worldOnly)
                foreach (var category in Ranking.Ranks.Keys.Where(category => category != Ranking.Type.World).ToArray())
                    Ranking.Ranks.Remove(category);
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeRefreshGraphImage();
            var incoming = RankPublicationRow(99, total);
            var incomingPoints = incoming.Points;

            Ranking.PublishPrestigeEntry(incoming, includeClass: !worldOnly);

            prior.RequireUnchanged();
            Assert.Equal(total, incoming.TotalPoints);
            Assert.Same(incomingPoints, incoming.Points);
            Assert.Equal(new uint[] { 7, 8 }, incoming.Points);
            Assert.Null(Ranking.GetInfo(99));
        });

    [Fact]
    public void PrestigeRefreshDeclineDoesNotInventBackfillFromDiscardedRowsOrAnotherRetainedBucket() =>
        RunPrestigeClientPreparation(fixture =>
        {
            Ranking.Ranks[Ranking.Type.World].MaxItems = 2;
            Ranking.Ranks[Ranking.Type.Trojan].MaxItems = 1;
            PublishPrestigeRefreshRow(41, 50_000);
            var worldOnlySurvivor = PublishPrestigeRefreshRow(42, 40_000);
            var discarded = PublishPrestigeRefreshRow(43, 30_000);
            Assert.Equal(new uint[] { 41, 42 }, Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Equal(new uint[] { 41 }, Ranking.Ranks[Ranking.Type.Trojan].Items.Keys);
            var prior = new PrestigeRefreshGraphImage();

            PublishPrestigeRefreshRow(41, 19_999);

            Assert.Empty(Ranking.Ranks[Ranking.Type.Trojan].Items);
            Assert.Null(Ranking.Ranks[Ranking.Type.Trojan].BestOfClass);
            Assert.Equal(new uint[] { 42 }, Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Same(worldOnlySurvivor, Ranking.BestOfTheWorld);
            Assert.Null(Ranking.GetInfo(discarded.UID));
            Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Trojan, 42));
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });
}
