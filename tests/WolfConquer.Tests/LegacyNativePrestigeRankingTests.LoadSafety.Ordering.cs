using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void LoadSafetyCompletePrestigeLoadKeepsDescendingWorldAndClassLimitsAndExcludesLowScores() =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            var rows = Enumerable.Range(0, 110)
                .Select(index => CompletePrestigeLoadRow((uint)(1000 + index), (uint)(20_000 + index)))
                .Append(CompletePrestigeLoadRow(5000, 19_999))
                .ToArray();
            var text = Text(rows);
            files.Seed(text);
            Ranking.Load();
            var world = Ranking.Ranks[Ranking.Type.World];
            var ninja = Ranking.Ranks[Ranking.Type.Ninja];
            Assert.Equal(Enumerable.Range(1010, 100).Reverse().Select(value => (uint)value), world.Items.Keys);
            Assert.Equal(Enumerable.Range(1080, 30).Reverse().Select(value => (uint)value), ninja.Items.Keys);
            Assert.Equal(1109U, Ranking.BestOfTheWorld!.UID);
            Assert.Same(ninja.Items[1109], world.Items[1109]);
            Assert.Same(ninja.Items[1109], ninja.BestOfClass);
            Assert.Equal(100U, Ranking.GetMyRank(Ranking.Type.World, 1010));
            Assert.Equal(30U, Ranking.GetMyRank(Ranking.Type.Ninja, 1080));
            Assert.Null(Ranking.GetInfo(1009));
            Assert.Null(Ranking.GetInfo(5000));
            prior.RequireEscapedOwnersUnchanged();
            files.Expect(text);
        });

    [Fact]
    public void LoadSafetyCompletePrestigeLoadPreservesSavedTieOrderInclusiveThresholdAndSharedWorldClassIdentity() =>
        RunOwnedPrestigeLoad(files =>
        {
            var text = Text(CompletePrestigeLoadRow(17, 30_000),
                CompletePrestigeLoadRow(19, 30_000, category: 3),
                CompletePrestigeLoadRow(23, 30_000), CompletePrestigeLoadRow(29, 20_000));
            files.Seed(text);
            Ranking.Load();
            var world = Ranking.Ranks[Ranking.Type.World];
            var ninja = Ranking.Ranks[Ranking.Type.Ninja];
            Assert.Equal(new uint[] { 17, 19, 23, 29 }, world.Items.Keys);
            Assert.Equal(new uint[] { 17, 23, 29 }, ninja.Items.Keys);
            Assert.Equal(17U, Ranking.BestOfTheWorld!.UID);
            Assert.Same(ninja.Items[17], Ranking.BestOfTheWorld);
            Assert.Same(ninja.Items[23], world.Items[23]);
            Assert.Equal(19U, Assert.Single(Ranking.Ranks[Ranking.Type.Archer].Items).Key);
            Assert.Equal(3U, Ranking.GetMyRank(Ranking.Type.Ninja, 29));
            files.Expect(text);
        });
}
