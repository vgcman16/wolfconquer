using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MutationSafetyDetachedChampionOnlyUidRefreshOrRemovalSelectsActualSurvivors(bool decline, bool hasSurvivor) =>
        RunPrestigeMutationSafety(() =>
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
            var removed = Entry(41, 50_000);
            var survivor = hasSurvivor ? Entry(42, 40_000) : null;
            rank.AddItem(removed);
            if (survivor is not null) rank.AddItem(survivor);
            Assert.True(rank.Items.Remove(41));
            Assert.Same(removed, rank.BestOfClass);
            var prior = new PrestigeMutationRankImage(rank);

            if (decline) rank.AddItem(Entry(41, 19_999));
            else rank.Remove(41);

            Assert.Same(survivor, rank.BestOfClass);
            Assert.Equal(hasSurvivor ? new uint[] { 42 } : [], rank.Items.Keys);
            prior.RequireEscapedContentsUnchanged();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutationSafetyGlobalChampionOnlyUidRemovalRefreshesEveryAffectedOwner(bool hasSurvivor) =>
        RunPrestigeMutationSafety(() =>
        {
            var removed = Entry(41, 50_000);
            var survivor = hasSurvivor ? Entry(42, 40_000) : null;
            foreach (var category in new[] { Ranking.Type.World, Ranking.Type.Ninja })
            {
                var owner = AddPrestigeMutationOwner(category, removed);
                if (survivor is not null) owner.AddItem(survivor);
                Assert.True(owner.Items.Remove(41));
                Assert.Same(removed, owner.BestOfClass);
            }
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeMutationRegistryImage();

            Ranking.Remove(41);

            foreach (var owner in Ranking.Ranks.Values)
            {
                Assert.Same(survivor, owner.BestOfClass);
                Assert.Equal(hasSurvivor ? new uint[] { 42 } : [], owner.Items.Keys);
            }
            Assert.Null(Ranking.GetInfo(41));
            prior.RequireRegistryAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void MutationSafetyDetachedAbsentUidDoesNotHealAnUnrelatedHistoricalChampion() =>
        RunPrestigeMutationSafety(() =>
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
            rank.AddItem(Entry(41, 50_000));
            Assert.True(rank.Items.Remove(41));
            var prior = new PrestigeMutationRankImage(rank);

            rank.AddItem(Entry(99, 19_999));
            prior.RequireUnchanged();
            rank.Remove(99);
            prior.RequireUnchanged();
        });
}
