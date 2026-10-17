using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMailNotifyPacketTests
{
    [Fact]
    public void LegacyMailNotifyOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2066, WorldPacketOpcodes.LegacyMailNotify);
        Assert.Equal(WorldPacketOpcodes.LegacyMailNotify, LegacyMailNotifyPacket.Opcode);
    }

    [Fact]
    public void LegacyMailNotifyStartupNoMailWritesSingleModeByte()
    {
        var packet = LegacyMailNotifyPacket.StartupNoMail;

        Assert.Equal(LegacyMailNotifyMode.NoMail, packet.Mode);
        Assert.Equal("0500120802", Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyMailNotifyStartupHasMailWritesSingleModeByte()
    {
        var packet = LegacyMailNotifyPacket.Startup(hasMail: true);

        Assert.Equal(LegacyMailNotifyMode.HasMail, packet.Mode);
        Assert.Equal("0500120803", Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyMailNotifyRoundTrips()
    {
        var packet = LegacyMailNotifyPacket.Startup(hasMail: true);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMailNotifyPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyMailNotifyRejectsUnknownMode()
    {
        var writer = new PacketWriter(LegacyMailNotifyPacket.Opcode, LegacyMailNotifyPacket.PayloadLength);
        writer.Payload[0] = 9;

        Assert.False(LegacyMailNotifyPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
