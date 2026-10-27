using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Fact]
    public void PrestigeRefreshEligibleWorldOnlyEntryAcceptsARegistryContainingOnlyItsActualWorldOwner() =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(41, 50_000, Ranking.Type.World, 10000, includeClass: false);
            foreach (var category in Ranking.Ranks.Keys.Where(category => category != Ranking.Type.World).ToArray())
                Ranking.Ranks.Remove(category);
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeRefreshGraphImage();

            var current = PublishPrestigeRefreshRow(41, 60_000, Ranking.Type.World, 10000, includeClass: false);

            Assert.Equal(new[] { Ranking.Type.World }, Ranking.Ranks.Keys);
            Assert.Same(current, Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value);
            Assert.Same(current, Ranking.BestOfTheWorld);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void PrestigeRefreshExactThresholdRetainsTheIncomingEntryInWorldAndClass() =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(41, 50_000);
            var peer = PublishPrestigeRefreshRow(42, 20_000);
            var prior = new PrestigeRefreshGraphImage();

            var current = PublishPrestigeRefreshRow(41, 20_000);

            Assert.Equal(new uint[] { 41, 42 }, Ranking.Ranks[Ranking.Type.World].Items.Keys);
            Assert.Equal(new uint[] { 41, 42 }, Ranking.Ranks[Ranking.Type.Trojan].Items.Keys);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
            Assert.Same(peer, Ranking.Ranks[Ranking.Type.World].Items[42]);
            Assert.Same(current, Ranking.BestOfTheWorld);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void PrestigeRefreshSharedWorldAndClassDictionaryUsesCopyOnWriteWithoutRejectingValidMirrors() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var old = PublishPrestigeRefreshRow(41, 50_000);
            var world = Ranking.Ranks[Ranking.Type.World];
            var classRank = Ranking.Ranks[Ranking.Type.Trojan];
            classRank.Items = world.Items;
            var shared = world.Items;
            Assert.NotSame(world, classRank);
            var prior = new PrestigeRefreshGraphImage();

            var current = PublishPrestigeRefreshRow(41, 60_000);

            Assert.NotSame(shared, world.Items);
            Assert.NotSame(shared, classRank.Items);
            Assert.Same(old, Assert.Single(shared).Value);
            Assert.Same(current, Assert.Single(world.Items).Value);
            Assert.Same(current, Assert.Single(classRank.Items).Value);
            Assert.Same(current, world.BestOfClass);
            Assert.Same(current, classRank.BestOfClass);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void PrestigeRefreshWorldAndClassMirrorsNeedNotBeTheSamePhysicalEntryOrAppearance() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var worldRow = PublishPrestigeRefreshRow(41, 50_000);
            var classRow = RankPublicationRow(41, 50_000);
            classRow.Name = "Class mirror";
            classRow.Mesh = 123;
            classRow.Points = [91, 92, 93];
            Ranking.Ranks[Ranking.Type.Trojan].Items[41] = classRow;
            Ranking.Ranks[Ranking.Type.Trojan].BestOfClass = classRow;
            Assert.NotSame(worldRow, classRow);
            var prior = new PrestigeRefreshGraphImage();

            var current = PublishPrestigeRefreshRow(41, 60_000);

            Assert.Same(current, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
            Assert.Same(current, Ranking.BestOfTheWorld);
            prior.RequireOwnersAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });
}
