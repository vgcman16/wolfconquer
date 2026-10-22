using WolfConquer.Core.World;
using MapType = WolfConquer.Core.World.LegacyNativeMapObjectType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapViewTests
{
    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(18, 36, 1, 2)]
    [InlineData(19, 37, 2, 3)]
    [InlineData(36, 18, 2, 1)]
    [InlineData(0, 0, 1, 1)]
    public void GridUsesNativeEighteenCellRoundingAndDistinctBlockOwners(int width, int height, int blocksX, int blocksY)
    {
        var map = new LegacyNativeMapView(width, height);

        Assert.Equal(blocksX, map.m_setBlock.GetLength(0));
        Assert.Equal(blocksY, map.m_setBlock.GetLength(1));
        var blocks = map.m_setBlock.Cast<LegacyNativeViewPtr>().ToArray();
        Assert.Equal(blocksX * blocksY, blocks.Distinct().Count());
        Assert.All(blocks, block => Assert.Empty(block.GetObjects(MapType.Player)));
    }

    [Fact]
    public void PlayerScreenCounterStartsAtOneButFirstPreincrementIsTwoAndNonPlayersDoNotConsumeIt()
    {
        var map = new LegacyNativeMapView(36, 36);
        var first = Model(1, type: MapType.Player);
        var second = Model(2, 1, 1, MapType.Player);
        var third = Model(3, 2, 2, MapType.Player);
        var monster = Model(4);
        monster.IndexInScreen = 70;

        Assert.True(map.EnterMap(first));
        Assert.Equal(2u, first.IndexInScreen);
        Assert.True(map.EnterMap(monster));
        Assert.Equal(70u, monster.IndexInScreen);
        Assert.True(map.EnterMap(second));
        Assert.Equal(3u, second.IndexInScreen);
        Assert.True(map.MoveTo(first, 18, 18));
        Assert.Equal(4u, first.IndexInScreen);
        Assert.False(map.MoveTo(second, 17, 17));
        Assert.Equal(3u, second.IndexInScreen);
        Assert.True(map.EnterMap(third));
        Assert.Equal(5u, third.IndexInScreen);
    }

    [Fact]
    public void RepeatedEnterAddsDuplicatesAndLeaveRemovesOneWithoutMembershipResult()
    {
        var map = new LegacyNativeMapView(18, 18);
        var player = Model(1, type: MapType.Player);
        var list = map.m_setBlock[0, 0].GetObjects(MapType.Player);

        Assert.True(map.EnterMap(player));
        Assert.True(map.EnterMap(player));
        Assert.Equal(3u, player.IndexInScreen);
        Assert.Equal(new ILegacyNativeMapObj[] { player, player }, list);
        Assert.True(map.LeaveMap(player));
        Assert.Same(player, Assert.Single(list));
        Assert.True(map.LeaveMap(player));
        Assert.Empty(list);
        Assert.True(map.LeaveMap(player)); // Native return does not report Remove's result.
        Assert.Equal(3u, player.IndexInScreen);
    }

    [Fact]
    public void SameBlockAndInvalidCoordinatesReturnFalseWithoutMovingOrAdvancingCounter()
    {
        var map = new LegacyNativeMapView(36, 36);
        var player = Model(1, type: MapType.Player);
        Assert.True(map.EnterMap(player));
        var list = map.m_setBlock[0, 0].GetObjects(MapType.Player);

        foreach (var target in new[] { (17, 17), (-1, 0), (36, 0), (0, -1), (0, 36) })
            Assert.False(map.MoveTo(player, target.Item1, target.Item2));
        var invalid = Model(2, 36, 0, MapType.Player);
        invalid.IndexInScreen = 90;
        Assert.False(map.EnterMap(invalid));
        Assert.False(map.LeaveMap(invalid));
        Assert.False(map.MoveTo(invalid, 1, 1));
        Assert.Equal(90u, invalid.IndexInScreen);
        Assert.Same(player, Assert.Single(list));
        Assert.Equal((ushort)0, player.X);
        Assert.Equal((ushort)0, player.Y);
        Assert.Equal(2u, player.IndexInScreen);
        Assert.True(map.EnterMap(Model(3, type: MapType.Player)));
        Assert.Equal(3u, list[1].IndexInScreen);
    }

    [Fact]
    public void CrossBlockMoveUsesOldCoordinatesButDoesNotWriteThemAndLeaveCanMissNewBucket()
    {
        var map = new LegacyNativeMapView(36, 36);
        var player = Model(1, 1, 1, MapType.Player);
        Assert.True(map.EnterMap(player));
        var old = map.m_setBlock[0, 0].GetObjects(MapType.Player);
        var next = map.m_setBlock[1, 1].GetObjects(MapType.Player);

        Assert.True(map.MoveTo(player, 18, 18));

        Assert.Empty(old);
        Assert.Same(player, Assert.Single(next));
        Assert.Equal((ushort)1, player.X);
        Assert.Equal((ushort)1, player.Y);
        Assert.Equal(3u, player.IndexInScreen);
        Assert.True(map.LeaveMap(player));
        Assert.Same(player, Assert.Single(next));
        player.X = 18; // MODEL caller coordinate publication, not MapView behavior.
        player.Y = 18;
        Assert.True(map.LeaveMap(player));
        Assert.Empty(next);
    }
}
