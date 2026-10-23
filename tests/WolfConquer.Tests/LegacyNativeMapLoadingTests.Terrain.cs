using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Theory]
    [InlineData(1038, 0u, true)]
    [InlineData(1002, 1038u, false)]
    [InlineData(1038, 1002u, true)]
    public Task WholeMapLoaderRetainsXyCellOrderSignedTerrainAndLogicalGuildWarFloorGate(
        int id, uint baseId, bool floors) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            scope.WriteMap("terrain.dmap", 3, 2, (x, y) =>
                ((short)((x + y * 3) % 3 == 0 ? 0 : -1),
                    (short)(-30000 + x + y * 10), (short)(-1000 + x * 5 + y * 17)));

            Assert.True(LegacyNativeGameMap.LoadMap(id, "terrain.dmap", baseId));

            var map = scope.Loaded(id);
            Assert.Equal((uint)id, map.ID);
            Assert.Equal(baseId, map.BaseID);
            Assert.Equal(3, map.bounds.Width); Assert.Equal(2, map.bounds.Height);
            Assert.Equal(3, map.Altitude.GetLength(0)); Assert.Equal(2, map.Altitude.GetLength(1));
            Assert.Equal(floors, map.FloorType != null);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal((x + y * 3) % 3 == 0 ? Cell.Valid : Cell.None, map.cells[x, y]);
                Assert.Equal(-1000 + x * 5 + y * 17, map.Altitude[x, y]);
                if (floors) Assert.Equal(-30000 + x + y * 10, map.FloorType[x, y]);
            }
            Assert.NotNull(map.View);
            Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player));
            Assert.NotNull(map.MonstersColletion);
            Assert.Null(map.MonstersColletion.DMap); // No synthesized post-publication bind.
            Assert.True(map.MonstersColletion.ReadMap());
            Assert.Same(map, map.MonstersColletion.DMap);
            Assert.Empty(scope.Queue);
            scope.AssertTail();
        });

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public Task WholeLoaderRetainsNativeZeroDimensionAcceptance(int width, int height) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            scope.WriteMap("empty.dmap", width, height);
            Assert.True(LegacyNativeGameMap.LoadMap(700, "empty.dmap"));
            var map = scope.Loaded(700);
            Assert.Equal(width, map.cells.GetLength(0));
            Assert.Equal(height, map.cells.GetLength(1));
            Assert.False(map.ValidLocation(0, 0));
            Assert.NotNull(map.View);
        });

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(-1, uint.MaxValue)]
    [InlineData(700, 700u)]
    public Task DirectLoaderRetainsRawSignedIdCastAndDoesNotInventLazyEnterMapPolicy(int id, uint expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            scope.WriteMap("id.dmap", 1, 1);
            Assert.True(LegacyNativeGameMap.LoadMap(id, "id.dmap"));
            Assert.Equal(expected, scope.Loaded(id).ID);
        });

    [Fact]
    public Task NativeLoaderIgnoresHeaderBytesRowChecksumAndTrailingDmapSections() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        scope.WriteMap("tail.dmap", 2, 1, tail: [1, 2, 3, 4, 5]);
        Assert.True(LegacyNativeGameMap.LoadMap(700, "tail.dmap"));
        Assert.Equal(Cell.Valid, scope.Loaded(700).cells[1, 0]);
    });

    [Fact]
    public Task DuplicateIdReturnsTrueAndRebindsExistingMapMonsterOwnerWithoutReplacingPublishedMap() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var old = Map(7, 7, 700);
            var oldMob = new LegacyNativeMobCollection(700);
            old.MonstersColletion = oldMob;
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add(700, old));
            scope.WriteMap("duplicate.dmap", 2, 2);

            Assert.True(LegacyNativeGameMap.LoadMap(700, "duplicate.dmap", 123));

            Assert.Same(old, scope.Loaded(700));
            Assert.Equal(7, old.bounds.Width);
            Assert.NotSame(oldMob, old.MonstersColletion);
            Assert.Same(old, old.MonstersColletion.DMap);
            Assert.Equal(0u, old.BaseID);
            Assert.Single(LegacyNativePoolMaps.ServerMaps.Base);
        });

    [Fact]
    public Task NullCatalogDoesNotGetRepairedAfterTerrainReadAndPublicationFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        scope.WriteMap("null-catalog.dmap", 2, 2);
        LegacyNativePoolMaps.ServerMaps = null;
        Assert.False(LegacyNativeGameMap.LoadMap(700, "null-catalog.dmap"));
        Assert.Null(LegacyNativePoolMaps.ServerMaps);
    });

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public Task InvalidDimensionsFailWithoutPublishingPlaceholderMap(int width, int height) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            scope.WriteMap("negative.dmap", width, height);
            Assert.False(LegacyNativeGameMap.LoadMap(700, "negative.dmap"));
            Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(267)]
    [InlineData(271)]
    [InlineData(275)]
    [InlineData(276)]
    [InlineData(281)]
    [InlineData(285)]
    public Task TruncatedHeaderOrCellRowsFailWithoutMapPublication(int length) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        byte[] complete = Dmap(2, 2);
        File.WriteAllBytes(Path.Combine(scope.ClientDirectory, "short.dmap"), complete.AsSpan(0, length).ToArray());
        Assert.False(LegacyNativeGameMap.LoadMap(700, "short.dmap"));
        Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
    });

    [Fact]
    public Task MissingMapFileReturnsFalseWithoutReadingOriginalClientPath() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        Assert.False(LegacyNativeGameMap.LoadMap(700, "owned-missing.dmap"));
        Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
    });
}
