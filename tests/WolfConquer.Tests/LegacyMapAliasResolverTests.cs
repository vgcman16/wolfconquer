using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyMapAliasResolverTests
{
    [Theory]
    [InlineData(44455)]
    [InlineData(44456)]
    [InlineData(44457)]
    public void ResolveMapsWindWalkerFirstStageAliasesToBaseTerrain(int requestedMapId)
    {
        var resolution = LegacyMapAliasResolver.Resolve(requestedMapId);

        Assert.True(resolution.IsAlias);
        Assert.Equal(requestedMapId, resolution.RequestedMapId);
        Assert.Equal(10088, resolution.TerrainSourceMapId);
        Assert.Equal(10088, resolution.BaseMapId);
        Assert.Equal(10088U, resolution.LegacyGameMapBaseId);
    }

    [Theory]
    [InlineData(44460)]
    [InlineData(44461)]
    [InlineData(44462)]
    [InlineData(44463)]
    public void ResolveMapsWindWalkerSecondStageAliasesToBaseTerrain(int requestedMapId)
    {
        var resolution = LegacyMapAliasResolver.Resolve(requestedMapId);

        Assert.True(resolution.IsAlias);
        Assert.Equal(requestedMapId, resolution.RequestedMapId);
        Assert.Equal(10090, resolution.TerrainSourceMapId);
        Assert.Equal(10090, resolution.BaseMapId);
        Assert.Equal(10090U, resolution.LegacyGameMapBaseId);
    }

    [Theory]
    [InlineData(44454)]
    [InlineData(44458)]
    [InlineData(44459)]
    [InlineData(44464)]
    public void ResolveLeavesMapsOutsideWindWalkerAliasRangesUnchanged(int requestedMapId)
    {
        var resolution = LegacyMapAliasResolver.Resolve(requestedMapId);

        Assert.False(resolution.IsAlias);
        Assert.Equal(requestedMapId, resolution.RequestedMapId);
        Assert.Equal(requestedMapId, resolution.TerrainSourceMapId);
        Assert.Equal(requestedMapId, resolution.BaseMapId);
        Assert.Equal(0U, resolution.LegacyGameMapBaseId);
    }

    [Fact]
    public void ResolveLeavesActualBaseMapsUnchangedWithZeroLegacyBaseId()
    {
        var firstStage = LegacyMapAliasResolver.Resolve(10088);
        var secondStage = LegacyMapAliasResolver.Resolve(10090);

        Assert.False(firstStage.IsAlias);
        Assert.Equal(10088, firstStage.TerrainSourceMapId);
        Assert.Equal(10088, firstStage.BaseMapId);
        Assert.Equal(0U, firstStage.LegacyGameMapBaseId);

        Assert.False(secondStage.IsAlias);
        Assert.Equal(10090, secondStage.TerrainSourceMapId);
        Assert.Equal(10090, secondStage.BaseMapId);
        Assert.Equal(0U, secondStage.LegacyGameMapBaseId);
    }

    [Fact]
    public void TryResolveAliasReportsWhetherRequestedMapUsesAnotherTerrainSource()
    {
        Assert.True(LegacyMapAliasResolver.TryResolveAlias(44463, out var alias));
        Assert.Equal(new LegacyMapAliasResolution(44463, 10090), alias);

        Assert.False(LegacyMapAliasResolver.TryResolveAlias(10090, out var direct));
        Assert.Equal(new LegacyMapAliasResolution(10090, 10090), direct);
    }

    [Theory]
    [InlineData(5040, 5000)]
    [InlineData(6546, 3872)]
    [InlineData(6521, 2071)]
    [InlineData(6891, 2071)]
    [InlineData(6729, 3825)]
    [InlineData(3830, 1780)]
    [InlineData(3829, 3825)]
    [InlineData(10429, 10428)]
    public void ResolveIncludesLegacyGameMapEnterMapAliases(int requestedMapId, int expectedTerrainSourceMapId)
    {
        var resolution = LegacyMapAliasResolver.Resolve(requestedMapId);

        Assert.True(resolution.IsAlias);
        Assert.Equal(expectedTerrainSourceMapId, resolution.TerrainSourceMapId);
        Assert.Contains(LegacyMapAliasResolver.KnownAliases, alias => alias == resolution);
    }
}
