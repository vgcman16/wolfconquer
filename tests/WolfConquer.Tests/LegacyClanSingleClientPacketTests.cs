using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanSingleClientPacketTests
{
    [Fact]
    public void LegacyClanSingleClientOpcodeMatchesLegacyClanOpcode()
    {
        Assert.Equal((ushort)2049, WorldPacketOpcodes.LegacyClan);
        Assert.Equal(WorldPacketOpcodes.LegacyClan, LegacyClanSingleClientPacket.Opcode);
    }

    [Fact]
    public void LegacyClanSingleClientJoinRequestRoundTripsSourceShape()
    {
        // Mirrors MsgClan.ClanAppendSingleClientCreate(client.Player.UID, client.Player.Name, Info.Join).
        var packet = LegacyClanSingleClientPacket.Create(mode: 11, uid: 1_293_600, name: "HeroOne");
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(WorldPacketOpcodes.LegacyClan, frame.Header.Opcode);
        Assert.Equal(22, frame.Payload.Length);

        Assert.True(LegacyClanSingleClientPacket.TryParse(frame, out var parsed));
        Assert.Equal(11U, parsed.Mode);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(0U, parsed.Parameter);
        Assert.Equal("HeroOne", parsed.Name);
    }

    [Fact]
    public void LegacyClanSingleClientPacketRejectsWrongOpcode()
    {
        var wrongOpcode = new PacketWriter((ushort)(LegacyClanSingleClientPacket.Opcode + 1), payloadLength: 22);

        Assert.False(LegacyClanSingleClientPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
