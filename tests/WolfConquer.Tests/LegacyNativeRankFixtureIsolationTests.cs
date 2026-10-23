using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePerfectionEffectTests
{
    [Fact]
    public void NestedPerfectionFixturePreservesEscapedCatalogAndSharedSeedStream()
    {
        using var outer = new Fixture();
        var catalog = LegacyNativePoolItems.ItemsBase;
        var random = LegacyNativePoolRandom.GetRandom;
        var control = new LegacyNativeSafeRandom(71);
        _ = control.Next(); // The outer Player's actual constructor seed draw.
        using (var inner = new Fixture())
        {
            Assert.NotSame(catalog, LegacyNativePoolItems.ItemsBase);
            Assert.NotSame(random, LegacyNativePoolRandom.GetRandom);
            _ = LegacyNativePoolRandom.GetRandom.Next();
        }
        Assert.Same(catalog, LegacyNativePoolItems.ItemsBase);
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(control.Next(), random.Next());
    }
}

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void NestedPrestigeFixturePreservesRankOwnersAndSharedSeedStream()
    {
        using var outer = new State();
        var ranks = WolfConquer.Core.Data.LegacyNativePrestigeRanking.Ranks;
        var random = LegacyNativePoolRandom.GetRandom;
        var control = new LegacyNativeSafeRandom(71);
        using (var inner = new State())
        {
            Assert.NotSame(ranks, WolfConquer.Core.Data.LegacyNativePrestigeRanking.Ranks);
            Assert.NotSame(random, LegacyNativePoolRandom.GetRandom);
            _ = new LegacyNativeGameClientRuntime();
        }
        Assert.Same(ranks, WolfConquer.Core.Data.LegacyNativePrestigeRanking.Ranks);
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(control.Next(), random.Next());
    }
}
