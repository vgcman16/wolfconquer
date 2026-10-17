using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVipHandlerPacketTests
{
    [Fact]
    public void OpcodeMatchesLegacySource()
    {
        Assert.Equal((ushort)2126, WorldPacketOpcodes.LegacyVipHandler);
        Assert.Equal(WorldPacketOpcodes.LegacyVipHandler, LegacyVipHandlerPacket.Opcode);
    }

    [Fact]
    public void PacketRoundTripsLegacyFields()
    {
        var packet = new LegacyVipHandlerPacket(
            LegacyVipTeleportType.TeamTeleport,
            LegacyVipTeleportLocation.DCSquare,
            Countdown: 15,
            Name: "HeroOne");

        Assert.True(LegacyVipHandlerPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void PacketSupportsEmptyNameRequests()
    {
        var packet = new LegacyVipHandlerPacket(
            LegacyVipTeleportType.SelfTeleport,
            LegacyVipTeleportLocation.PhoenixCastle,
            Countdown: 0,
            Name: "");

        Assert.True(LegacyVipHandlerPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyVipTeleportType.SelfTeleport, parsed.TeleportType);
        Assert.Equal(LegacyVipTeleportLocation.PhoenixCastle, parsed.Location);
        Assert.Equal(0U, parsed.Countdown);
        Assert.Equal("", parsed.Name);
    }

    [Fact]
    public void RejectsMalformedStringLength()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyVipHandler, LegacyVipHandlerPacket.MinimumPayloadLength);
        writer.WriteUInt32(0, (uint)LegacyVipTeleportType.SelfTeleport);
        writer.WriteUInt32(4, (uint)LegacyVipTeleportLocation.TwinCity);
        writer.WriteUInt32(8, 0);
        writer.WriteUInt16(12, 1);

        Assert.False(LegacyVipHandlerPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
