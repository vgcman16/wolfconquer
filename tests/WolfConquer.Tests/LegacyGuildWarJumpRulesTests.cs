using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildWarJumpRulesTests
{
    [Fact]
    public void TargetNearRightGateCenterResetsTerrainMaskAndAllowsJump()
    {
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask: 3,
            targetX: 220,
            targetY: 180,
            floorType: 13,
            leftGateOpen: false,
            rightGateOpen: false,
            out var nextTerrainMask);

        Assert.True(allowed);
        Assert.Equal(0, nextTerrainMask);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(13)]
    public void CurrentThreeToGateFloorAwayFromOpenGatesBlocksJump(int floorType)
    {
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask: 3,
            targetX: 190,
            targetY: 190,
            floorType: floorType,
            leftGateOpen: false,
            rightGateOpen: false,
            out var nextTerrainMask);

        Assert.False(allowed);
        Assert.Equal(3, nextTerrainMask);
    }

    [Fact]
    public void LeftGateOpenAllowsCurrentThreeToGateFloorNearLeftGate()
    {
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask: 3,
            targetX: 164,
            targetY: 209,
            floorType: 9,
            leftGateOpen: true,
            rightGateOpen: false,
            out var nextTerrainMask);

        Assert.True(allowed);
        Assert.Equal(9, nextTerrainMask);
    }

    [Fact]
    public void RightGateOpenAllowsCurrentThreeToGateFloorNearRightGate()
    {
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask: 3,
            targetX: 222,
            targetY: 177,
            floorType: 13,
            leftGateOpen: false,
            rightGateOpen: true,
            out var nextTerrainMask);

        Assert.True(allowed);
        Assert.Equal(13, nextTerrainMask);
    }

    [Fact]
    public void NonGateFloorTypeUsesNormalFloorTransition()
    {
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask: 3,
            targetX: 190,
            targetY: 190,
            floorType: 5,
            leftGateOpen: false,
            rightGateOpen: false,
            out var nextTerrainMask);

        Assert.True(allowed);
        Assert.Equal(5, nextTerrainMask);
    }
}
