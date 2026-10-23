using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, false, true)]
    [InlineData(3, true, true)]
    [InlineData(16, true, false)]
    [InlineData(18, true, true)]
    [InlineData(128, false, false)]
    public void NativeLocationAndMonsterQueriesKeepDistinctFlagRules(int flags, bool valid, bool monster)
    {
        var map = Map(5, 5, fill: Cell.None);
        map.cells[0, 0] = (Cell)flags;
        Assert.Equal(valid, map.ValidLocation(0, 0));
        Assert.Equal(monster, map.MonsterOnTile(0, 0));
        Assert.False(map.ValidLocation(5, 0));
        Assert.False(map.MonsterOnTile(0, 5));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(4, 4, true)]
    [InlineData(5, 1, false)]
    [InlineData(-1, 1, false)]
    public void IsFlagPresentUsesStrictPositiveInteriorUnlikeValidLocation(int x, int y, bool expected)
    {
        var map = Map(5, 5);
        Assert.Equal(expected, map.IsFlagPresent(x, y, Cell.Valid));
        Assert.Equal(expected, map.IsFlagPresent(x, y, Cell.None));
    }

    [Fact]
    public void MonsterMutationOrsAndClearsOnlyMonsterBitWithoutReplacingOtherFlags()
    {
        var map = Map(5, 5, fill: Cell.Valid | Cell.Item | (Cell)128);
        map.SetMonsterOnTile(2, 2, true);
        Assert.Equal(Cell.Valid | Cell.Item | Cell.Monster | (Cell)128, map.cells[2, 2]);
        map.SetMonsterOnTile(2, 2, false);
        Assert.Equal(Cell.Valid | Cell.Item | (Cell)128, map.cells[2, 2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NpcAndGateFlagsOverwriteFiveByFiveAreaAndKeepNativeLastBorderClipping(bool gate)
    {
        var map = Map(7, 7, fill: Cell.Valid | Cell.Monster | Cell.Item);
        if (gate) map.SetGateFlagNpc(4, 4);
        else map.SetFlagNpc(4, 4);
        for (int x = 0; x < 7; x++)
        for (int y = 0; y < 7; y++)
        {
            bool replaced = x >= 2 && x <= 5 && y >= 2 && y <= 5;
            Assert.Equal(replaced ? (gate ? Cell.None : Cell.Npc) : Cell.Valid | Cell.Monster | Cell.Item,
                map.cells[x, y]);
        }
    }

    [Fact]
    public void RemovingNpcOverwritesThreeByThreeWithValidAndRetainsOuterNpcRing()
    {
        var map = Map(7, 7, fill: Cell.None);
        map.SetFlagNpc(3, 3);
        map.RemoveFlagNpc(3, 3);
        for (int x = 1; x <= 5; x++)
        for (int y = 1; y <= 5; y++)
            Assert.Equal(x >= 2 && x <= 4 && y >= 2 && y <= 4 ? Cell.Valid : Cell.Npc, map.cells[x, y]);
    }

    [Fact]
    public void NpcFlagCenterFaultIsNotSilentlyNormalized()
    {
        var map = Map(7, 7);
        Assert.Throws<IndexOutOfRangeException>(() => map.SetFlagNpc(7, 1));
        Assert.Throws<IndexOutOfRangeException>(() => map.SetGateFlagNpc(7, 1));
        Assert.Throws<IndexOutOfRangeException>(() => map.RemoveFlagNpc(7, 1));
        Assert.Equal(Cell.Valid, map.cells[6, 1]);
    }

    [Fact]
    public void IsValidFlagNpcRetainsSourceRepeatedCenterCheckRatherThanCheckingNeighbors()
    {
        var map = Map(9, 9, fill: Cell.None);
        map.cells[4, 4] = Cell.Valid;
        Assert.True(map.IsValidFlagNpc(4, 4));
        Assert.False(map.IsValidFlagNpc(0, 0));
        Assert.False(map.IsValidFlagNpc(3, 4));
        Assert.Equal(Cell.None, map.cells[3, 4]);
    }

    [Theory]
    [InlineData(516074u, true)]
    [InlineData(516075u, true)]
    [InlineData(516076u, true)]
    [InlineData(516077u, true)]
    [InlineData(516078u, true)]
    [InlineData(516079u, true)]
    [InlineData(516080u, true)]
    [InlineData(516073u, false)]
    [InlineData(516081u, false)]
    public void NativeGatePredicateUsesEveryLiteralAndRejectsNeighbors(uint uid, bool expected) =>
        Assert.Equal(expected, LegacyNativeGameMap.IsGate(uid));

    [Theory]
    [InlineData(1762u, true)]
    [InlineData(1927u, true)]
    [InlineData(1999u, true)]
    [InlineData(2054u, true)]
    [InlineData(2055u, true)]
    [InlineData(2056u, true)]
    [InlineData(2053u, false)]
    [InlineData(2057u, false)]
    public void NativeFrozenGrottoPredicateUsesLiteralMaps(uint id, bool expected) =>
        Assert.Equal(expected, LegacyNativeGameMap.IsFrozengrotoMaps(id));
}
