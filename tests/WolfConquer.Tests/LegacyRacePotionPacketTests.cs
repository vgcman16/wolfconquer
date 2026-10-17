using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRacePotionPacketTests
{
    [Fact]
    public void LegacyRacePotionOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2180, WorldPacketOpcodes.LegacyRacePotion);
        Assert.Equal(WorldPacketOpcodes.LegacyRacePotion, LegacyRacePotionPacket.Opcode);
    }

    [Fact]
    public void LegacyRacePotionWritesLegacyStructLayout()
    {
        var packet = new LegacyRacePotionPacket(
            Amount: 3,
            PotionType: LegacyRaceItemType.GuardPotion,
            Location: 2,
            DwParam: 0xA0B0C0D0);

        Assert.Equal(
        [
            0x10, 0x00, 0x84, 0x08,
            0x03, 0x00,
            0x90, 0x20,
            0x02, 0x00, 0x00, 0x00,
            0xD0, 0xC0, 0xB0, 0xA0,
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyRacePotionRoundTripsFields()
    {
        var packet = new LegacyRacePotionPacket(
            Amount: 1,
            PotionType: LegacyRaceItemType.ExcitementPotion,
            Location: 5,
            DwParam: 1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRacePotionPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyRacePotionRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyRacePotionPacket.Opcode + 1, LegacyRacePotionPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRacePotionPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRacePotionRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyRacePotionPacket.Opcode, LegacyRacePotionPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRacePotionPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRacePotionRejectsUnknownPotionType()
    {
        var writer = new PacketWriter(LegacyRacePotionPacket.Opcode, LegacyRacePotionPacket.PayloadLength);
        writer.WriteUInt16(0, 1);
        writer.WriteUInt16(2, 9000);
        writer.WriteInt32(4, 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRacePotionPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
