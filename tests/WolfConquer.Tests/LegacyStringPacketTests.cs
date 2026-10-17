using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStringPacketTests
{
    [Fact]
    public void LegacyStringOpcodeMatchesLegacyMsgStringPacketOpcode()
    {
        Assert.Equal((ushort)2036, WorldPacketOpcodes.LegacyString);
        Assert.Equal(WorldPacketOpcodes.LegacyString, LegacyStringPacket.Opcode);
    }

    [Fact]
    public void PlayerEffectWritesLegacyStringListLayout()
    {
        var packet = LegacyStringPacket.PlayerEffect(0x01020304, "eidolon");

        Assert.Equal(
        [
            0x13, 0x00, 0xF4, 0x07,
            0x04, 0x03, 0x02, 0x01,
            0x0A,
            0x01,
            0x07, 0x00,
            0x65, 0x69, 0x64, 0x6F, 0x6C, 0x6F, 0x6E,
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void PlayerEffectRoundTripsFields()
    {
        var packet = LegacyStringPacket.PlayerEffect(1_293_600, "eidolon");

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(LegacyStringId.Effect, parsed.Id);
        Assert.Equal(["eidolon"], parsed.Strings);
    }

    [Fact]
    public void LegacyStringRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyString + 1, payloadLength: 0);

        Assert.False(LegacyStringPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyStringRejectsDeclaredStringLengthMismatch()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyString, payloadLength: 8);
        writer.WriteUInt32(0, 1);
        writer.Payload[4] = (byte)LegacyStringId.Effect;
        writer.Payload[5] = 1;
        writer.WriteUInt16(6, 2);

        Assert.False(LegacyStringPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
