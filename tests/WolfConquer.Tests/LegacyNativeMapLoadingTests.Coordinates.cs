using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Fact]
    public Task RandomCoordinatesUsesExactlyTwoDrawsWhenFirstTileIsValid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = Map(4, 5);
        int x = scope.Control.Next(4), y = scope.Control.Next(5);
        var result = map.RandomCoordinates();
        Assert.Equal((ushort)x, result.Item1); Assert.Equal((ushort)y, result.Item2);
        scope.AssertTail();
    });

    [Fact]
    public Task RandomCoordinatesRetainsNativeTenThousandRetryLimitAndReturnsLastInvalidTile() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map(4, 5, fill: Cell.None);
            int x = 0, y = 0;
            for (int draw = 0; draw <= 10000; draw++)
            {
                x = scope.Control.Next(4);
                y = scope.Control.Next(5);
            }
            var result = map.RandomCoordinates();
            Assert.Equal((ushort)x, result.Item1); Assert.Equal((ushort)y, result.Item2);
            Assert.Equal(Cell.None, map.cells[result.Item1, result.Item2]);
            scope.AssertTail();
        });

    [Fact]
    public Task FullMapRandomCoordinatesKeepsSourceTwentyTileLowerMarginAndExclusiveLastBorder() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map(40, 41);
            ushort x = 1, y = 2;
            ushort expectedX = (ushort)scope.Control.Next(20, 39);
            ushort expectedY = (ushort)scope.Control.Next(20, 40);
            map.GetRandCoord(ref x, ref y);
            Assert.Equal(expectedX, x); Assert.Equal(expectedY, y);
            scope.AssertTail();
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task RadiusCoordinatesUseCurrentReferencesAndGenuineSharedDraws(int radius) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map();
            ushort x = 16, y = 16;
            ushort expectedX = (ushort)scope.Control.Next(16 - radius, 16 + radius);
            ushort expectedY = (ushort)scope.Control.Next(16 - radius, 16 + radius);
            map.GetRandCoord(ref x, ref y, radius);
            Assert.Equal(expectedX, x); Assert.Equal(expectedY, y);
            scope.AssertTail();
        });

    [Fact]
    public Task RadiusRetryWalksFromLastRejectedPointRatherThanOriginalCenter() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.ResetRandom(1);
            var map = Map(fill: Cell.None);
            var path = new List<(ushort X, ushort Y)>();
            int cx = 16, cy = 16;
            for (int step = 0; step < 3; step++)
            {
                cx = scope.Control.Next(cx - 2, cx + 2);
                cy = scope.Control.Next(cy - 2, cy + 2);
                path.Add(((ushort)cx, (ushort)cy));
            }
            Assert.DoesNotContain(path[^1], path.Take(2));
            map.cells[path[^1].X, path[^1].Y] = Cell.Valid;
            ushort x = 16, y = 16;
            map.GetRandCoord(ref x, ref y, 2);
            Assert.Equal(path[^1], (x, y));
            scope.AssertTail();
        });

    [Fact]
    public Task NegativeRadiusFaultRetainsReferencesAndDoesNotAdvanceSharedRandom() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map();
            ushort x = 16, y = 17;
            Assert.Throws<ArgumentOutOfRangeException>(() => map.GetRandCoord(ref x, ref y, -1));
            Assert.Equal((ushort)16, x); Assert.Equal((ushort)17, y);
            scope.AssertTail();
        });

    [Fact]
    public Task DynamicIdsUseActualSharedCounterWithoutConstructingOrPublishingAMap() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeGameMap.DinamicIDS = new LegacyNativeCounter(77);
            var map = Map();
            Assert.Equal(78u, map.GenerateDynamicID());
            Assert.Equal(79u, map.GenerateDynamicID());
            Assert.Equal(79u, LegacyNativeGameMap.DinamicIDS.Count);
            Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
            Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
        });
}
