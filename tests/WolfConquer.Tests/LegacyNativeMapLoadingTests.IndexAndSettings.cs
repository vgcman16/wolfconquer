using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Fact]
    public Task IndexLoaderRetainsSignedIdsDuplicateLastWinsCaseSensitiveGlobalReplacementAndAscii() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            LegacyNativeGameMap.MapContents[777] = "retained";
            var bytes = Index((700, Name("dir.7z/map.7z"), -1), (700, Name("last.7Z"), 0),
                (-1, [0xFF, (byte)'.', (byte)'7', (byte)'z'], int.MaxValue),
                (0, Name("all.7z.7z"), 99));
            File.WriteAllBytes(Path.Combine(scope.ClientDirectory, "ini", "gamemap.dat"), bytes);

            LegacyNativeGameMap.LoadMaps();

            Assert.Equal("last.7Z", LegacyNativeGameMap.MapContents[700]);
            Assert.Equal("?.dmap", LegacyNativeGameMap.MapContents[-1]);
            Assert.Equal("all.dmap.dmap", LegacyNativeGameMap.MapContents[0]);
            Assert.Equal("retained", LegacyNativeGameMap.MapContents[777]);
            Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
            Assert.Empty(scope.Queue);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public Task NonpositiveIndexCountDoesNotClearExistingContents(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        LegacyNativeGameMap.MapContents[700] = "retained";
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        File.WriteAllBytes(Path.Combine(scope.ClientDirectory, "ini", "gamemap.dat"), bytes);
        LegacyNativeGameMap.LoadMaps();
        Assert.Equal("retained", Assert.Single(LegacyNativeGameMap.MapContents).Value);
    });

    [Fact]
    public Task IndexFailureRetainsEarlierPublishedEntryAndDoesNotReturnFakeSuccess() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            byte[] bytes = Index((700, Name("first.7z"), 0), (701, Name("second.7z"), 0));
            File.WriteAllBytes(Path.Combine(scope.ClientDirectory, "ini", "gamemap.dat"),
                bytes.AsSpan(0, bytes.Length - 1).ToArray());
            Assert.Throws<EndOfStreamException>(() => LegacyNativeGameMap.LoadMaps());
            Assert.Equal("first.dmap", Assert.Single(LegacyNativeGameMap.MapContents).Value);
            Assert.False(LegacyNativeGameMap.MapContents.ContainsKey(701));
        });

    [Fact]
    public Task NegativeIndexNameLengthFaultsBeforePublishingItsRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var bytes = Index((700, Name("first.7z"), 0));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), -1);
        File.WriteAllBytes(Path.Combine(scope.ClientDirectory, "ini", "gamemap.dat"), bytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeGameMap.LoadMaps());
        Assert.Empty(LegacyNativeGameMap.MapContents);
    });

    [Fact]
    public Task MissingIndexIsNotCaughtOrReplacedByEmptyCatalog() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        LegacyNativeGameMap.MapContents[700] = "retained";
        Assert.Throws<FileNotFoundException>(() => LegacyNativeGameMap.LoadMaps());
        Assert.Equal("retained", Assert.Single(LegacyNativeGameMap.MapContents).Value);
    });

    [Theory]
    [InlineData(true, 77u)]
    [InlineData(false, 0u)]
    public Task SettingsGateRetainsRawDbConcatenationRatherThanNormalizingMissingSeparator(
        bool trailingSeparator, uint expectedType) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles(trailingSeparator);
            scope.WriteMap("settings.dmap", 2, 2);
            File.WriteAllText(Path.Combine(scope.DbDirectory, "maps", "700.ini"),
                "[info]\r\ntype=77\r\nportal0_x=321\r\nportal0_y=456\r\nreborn_map=1002\r\nrace_record=999\r\ncolor=305419896\r\n",
                Encoding.ASCII);
            Assert.True(LegacyNativeGameMap.LoadMap(700, "settings.dmap"));
            var map = scope.Loaded(700);
            Assert.Equal(expectedType, map.TypeStatus);
            Assert.Equal(trailingSeparator ? (ushort)321 : (ushort)0, map.Reborn_X);
            Assert.Equal(trailingSeparator ? (ushort)456 : (ushort)0, map.Reborn_Y);
            Assert.Equal(trailingSeparator ? (ushort)1002 : (ushort)0, map.Reborn_Map);
            Assert.Equal(trailingSeparator ? 999u : 0u, map.RecordSteedRace);
            Assert.Equal(trailingSeparator ? 305419896u : 0u, map.MapColor);
        });

    [Fact]
    public Task BaseIdSelectsSettingsFileWithoutChangingLogicalMapIdOrFloorGate() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        scope.WriteMap("base.dmap", 2, 2);
        File.WriteAllText(Path.Combine(scope.DbDirectory, "maps", "1038.ini"), "[info]\r\ntype=123\r\n", Encoding.ASCII);
        File.WriteAllText(Path.Combine(scope.DbDirectory, "maps", "700.ini"), "[info]\r\ntype=456\r\n", Encoding.ASCII);
        Assert.True(LegacyNativeGameMap.LoadMap(700, "base.dmap", 1038));
        var map = scope.Loaded(700);
        Assert.Equal(700u, map.ID);
        Assert.Equal(1038u, map.BaseID);
        Assert.Equal(123u, map.TypeStatus);
        Assert.Null(map.FloorType);
    });

    [Fact]
    public Task MissingAndMalformedIniValuesRetainNativePerFieldDefaultReads() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        scope.WriteMap("bad-settings.dmap", 2, 2);
        File.WriteAllText(Path.Combine(scope.DbDirectory, "maps", "700.ini"),
            "[info]\r\ntype=bad\r\nportal0_x=65536\r\nportal0_y=123\r\nreborn_map=-1\r\nrace_record=not-a-number\r\n", Encoding.ASCII);
        Assert.True(LegacyNativeGameMap.LoadMap(700, "bad-settings.dmap"));
        var map = scope.Loaded(700);
        Assert.Equal(0u, map.TypeStatus);
        Assert.Equal((ushort)0, map.Reborn_X);
        Assert.Equal((ushort)123, map.Reborn_Y);
        Assert.Equal((ushort)0, map.Reborn_Map);
        Assert.Equal(0u, map.RecordSteedRace);
        Assert.Equal(0u, map.MapColor);
    });
}
