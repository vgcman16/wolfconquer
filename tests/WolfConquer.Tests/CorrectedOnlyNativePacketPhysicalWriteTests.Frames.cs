using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

public sealed unsafe partial class CorrectedOnlyNativePacketPhysicalWriteTests
{
    [Theory]
    [InlineData(null, "060034122211")]
    [InlineData("TQServer", "0600341222115451536572766572")]
    [InlineData("é1234567", "060034122211C3A9313233343536")]
    public Task FinalizeRetainsIndependentHeaderBodyAndNativeCapturedSealGolden(string? value, string hex) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(value);
        var expected = Convert.FromHexString(hex);
        using var packet = new Packet(expected.Length); packet.InitWriter();
        packet.Write((ushort)0x1122); packet.Finalize(0x1234);
        Packet.SealString = "Changed!";
        Assert.Equal(expected, Bytes(packet)); Assert.Equal(expected.Length, packet.Size);
        Assert.Equal(4, packet.Position); Assert.Equal(6, BitConverter.ToUInt16(Bytes(packet), 0));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task EmptyBodyAndMaximumUInt16DeclaredLengthFitExactPhysicalStorage(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(sealedFrame ? "TQServer" : null);
        var sealBytes = sealedFrame ? Convert.FromHexString("5451536572766572") : Array.Empty<byte>();
        using var minimal = new Packet(4 + sealBytes.Length); minimal.InitWriter(); minimal.Finalize(0x1234);
        Assert.Equal(Convert.FromHexString("04003412").Concat(sealBytes).ToArray(), Bytes(minimal));
        Assert.Equal(4 + sealBytes.Length, minimal.Size); Assert.Equal(4, minimal.Position);
        using var maximum = new Packet(65_535 + sealBytes.Length); Fill(maximum); maximum.Seek(65_535);
        maximum.Finalize(0x1234);
        var bytes = Bytes(maximum);
        Assert.Equal(Convert.FromHexString("FFFF3412"), bytes[..4]);
        Assert.All(bytes[4..65_535], value => Assert.Equal((byte)0xa5, value));
        Assert.Equal(sealBytes, bytes[65_535..]);
        Assert.Equal(65_535 + sealBytes.Length, maximum.Size); Assert.Equal(4, maximum.Position);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task FinalizeRejectsMissingReservedHeaderWithoutPublishingAnything(int cursor) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(null);
        using var packet = new Packet(8); Fill(packet); packet.Seek(cursor);
        Refuses<InvalidOperationException>(packet, () => packet.Finalize(0x1234));
    });

    [Fact]
    public Task FinalizeRejectsSealCapacityAndDeclaredLengthOverflowBeforeHeaderOrTailChanges() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope("TQServer");
        using var tooSmall = new Packet(13); Fill(tooSmall); tooSmall.Seek(6);
        Refuses<InvalidOperationException>(tooSmall, () => tooSmall.Finalize(0x1234));
        using var tooLong = new Packet(65_544); Fill(tooLong); tooLong.Seek(65_536);
        Refuses<InvalidOperationException>(tooLong, () => tooLong.Finalize(0x1234));
    });

    [Theory]
    [InlineData("TQServer", "5451536572766572")]
    [InlineData("é1234567", "C3A9313233343536")]
    public Task ExplicitSealWriteUsesCapturedEightBytesAndLeavesSizeUntouched(string value, string hex) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(value);
        using var exact = new Packet(8); Fill(exact); exact.WriteSeal();
        Assert.Equal(Convert.FromHexString(hex), Bytes(exact)); Assert.Equal(8, exact.Position); Assert.Equal(73, exact.Size);
        using var small = new Packet(7); Fill(small);
        Refuses<InvalidOperationException>(small, () => small.WriteSeal());
    });

    [Fact]
    public Task ExplicitMissingSealRefusesWithoutDirtyingDestination() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(null);
        using var packet = new Packet(8); Fill(packet);
        Refuses<InvalidOperationException>(packet, () => packet.WriteSeal());
    });
}
