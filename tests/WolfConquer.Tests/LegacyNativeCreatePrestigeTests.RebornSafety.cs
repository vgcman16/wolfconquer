using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData(1001u)]
    [InlineData(1052u)]
    public void RebornSafetyRealPrestigeRebuildKeepsTheFreshCurrentFamilyThroughHistoricalCleanup(uint currentClass) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            client.Player.Reborn = 1;
            client.Player.FirstClass = 1000;
            SavedField(client.Player, "_class", currentClass);
            // The real rebuild publishes the current row before CheckReborn,
            // matching the defect's equipment -> stats -> prestige caller order.
            client.CreatePrestigePoints();
            var fresh = client.PrestrigeEntry!;
            Assert.Equal(21_000u, fresh.TotalPoints); // Equipment20,000 + reborn1,000.
            Assert.Equal(currentClass, fresh.Class);
            Assert.Equal(Ranking.Type.Trojan, fresh.type);
            Assert.Same(fresh, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
            Assert.Same(fresh, Ranking.Ranks[Ranking.Type.World].Items[41]);
            var before = new PrestigeClientPreparationImage(client);

            Ranking.CheckReborn(client);

            before.RequireUnchanged();
            Assert.Same(fresh, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
            Assert.Same(fresh, Ranking.BestOfTheWorld);
        });
}
