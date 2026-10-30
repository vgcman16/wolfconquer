using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterDeathSupportTests
{
    [Theory]
    [InlineData(false, "0C008C080100000044332211")]
    [InlineData(true, "0C008C080000000044332211")]
    public Task BlackspotWriterUsesExactNativeInvertedActiveWord(bool active, string expected) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); Assert.Same(p.Stream, p.Stream.BlackspotCreate(active, 0x11223344));
        Assert.Equal(expected, Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        Assert.Equal(4, p.Stream.Position); Assert.Equal(12, p.Stream.Size);
    });
    [Fact]
    public Task BlackspotWriterOverwritesOldPayloadAndRetainsNativeSealFinalization() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Stream.InitWriter(); p.Stream.Write(new[] { "Old longer payload" }); p.Stream.Finalize(1);
        p.Stream.BlackspotCreate(false, uint.MaxValue);
        Assert.Equal("0C008C0801000000FFFFFFFF", Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        LegacyNativePacket.SealString = "WolfSeal"; p.Stream.BlackspotCreate(false, 17);
        Assert.Equal("0C008C080100000011000000576F6C665365616C", Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        Assert.Equal(20, p.Stream.Size); Assert.Equal(4, p.Stream.Position);
    });
    [Fact]
    public Task BlackspotNullStreamFaultsAtNativeWriterEntry() => NativePacketTestThread.Run(() =>
    {
        Assert.Throws<NullReferenceException>(() => LegacyNativeBlackspotPacket.BlackspotCreate(null!, false, 17));
    });
    [Theory]
    [InlineData(false, "0C00F4070100020009000000")]
    [InlineData(true, "0A00F407010002000900")]
    public Task MapEffectNullAndEmptyListsKeepTheNativeDistinctLayouts(bool empty, string expected) => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Client.Player.AddMapEffect(p.Stream, 1, 2, empty ? Array.Empty<string>() : null!);
        Assert.Equal(expected, Convert.ToHexString(Assert.Single(p.Queue)));
        Assert.Equal(expected, Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
    });
    [Fact]
    public Task MapEffectUsesLocationCoordinatesAndFullUtf8VariableString() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Client.Player.AddMapEffect(p.Stream, 10, 20, "狼");
        Assert.Equal("0F00F4070A00140009010300E78BBC", Convert.ToHexString(Assert.Single(p.Queue)));
    });
    [Fact]
    public Task MapEffectBroadcastUsesActualPlayerViewRatherThanEffectCoordinates() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); var map = new LegacyNativeGameMap(128, 128, 1002) { View = new LegacyNativeMapView(128, 128) };
        var nearQueue = new Queue<byte[]>(); var farQueue = new Queue<byte[]>(); var otherDynamicQueue = new Queue<byte[]>();
        var near = p.Peer(18, 65, 64, nearQueue); var far = p.Peer(19, 100, 100, farQueue); var otherDynamic = p.Peer(20, 65, 64, otherDynamicQueue);
        otherDynamic.Player.DynamicID = 8; map.Enquer(p.Client); map.Enquer(near); map.Enquer(far); map.Enquer(otherDynamic);
        p.Client.Player.AddMapEffect(p.Stream, 120, 120, "Wolf");
        byte[] owner = Assert.Single(p.Queue); Assert.Equal(owner, Assert.Single(nearQueue));
        Assert.Empty(farQueue); Assert.Empty(otherDynamicQueue);
        Assert.Equal((ushort)2036, Opcode(owner)); Assert.Equal((ushort)120, BitConverter.ToUInt16(owner, 4)); Assert.Equal((ushort)120, BitConverter.ToUInt16(owner, 6));
    });
    [Fact]
    public Task MapEffectBuildsPacketBeforeNullViewFaultButDoesNotSend() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Client.Player.View = null!;
        Assert.Throws<NullReferenceException>(() => p.Client.Player.AddMapEffect(p.Stream, 0x1122, 0x3344, "A"));
        Assert.Equal("0D00F407221144330901010041", Convert.ToHexString(NativeSpellTestFixture.Bytes(p.Stream)));
        Assert.Empty(p.Queue);
    });
    [Fact]
    public Task MapEffectOwnerSendPrecedesRealMissingMapViewFault() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Client.Map = new LegacyNativeGameMap(128, 128, 1002); // Source constructor really leaves View null.
        Assert.Throws<NullReferenceException>(() => p.Client.Player.AddMapEffect(p.Stream, 1, 2, "A"));
        Assert.Equal((ushort)2036, Opcode(Assert.Single(p.Queue)));
    });
    [Fact]
    public Task MapEffectNullStreamFaultsBeforeAnyOwnerPacket() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        byte[] before = NativeStringPacketTestScope.Bytes(p.Stream, p.Stream.Capacity);
        int position = p.Stream.Position, size = p.Stream.Size;
        Assert.Throws<ArgumentNullException>(() => p.Client.Player.AddMapEffect(null!, 1, 2, "A"));
        Assert.Empty(p.Queue); Assert.Equal(position, p.Stream.Position); Assert.Equal(size, p.Stream.Size);
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(p.Stream, p.Stream.Capacity));
    });
}
