using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemExtraPacketTests
{
    [Fact]
    public void LegacyItemExtraOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2134, WorldPacketOpcodes.LegacyItemExtra);
        Assert.Equal(WorldPacketOpcodes.LegacyItemExtra, LegacyItemExtraPacket.Opcode);
    }

    [Fact]
    public void LegacyItemExtraPacketWritesPermanentPurificationAndRefineryShape()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1002,
            PurificationItemId = 800001,
            PurificationLevel = 3,
            RefineryEffectId = 724440,
            RefineryEffectLevel = 2,
            RefineryEffectPercent = 10,
            RefineryEffectPercent2 = 20,
        };

        Assert.True(LegacyItemExtraPacket.TryCreate(item, DateTime.Now, out var packet));
        var bytes = packet.ToFrameBytes();

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacyItemExtraPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(64, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(2U, reader.ReadUInt32(0));
        Assert.Equal(1002U, reader.ReadUInt32(4));
        Assert.Equal(LegacyItemExtraPacket.StabilizationType, reader.ReadUInt32(8));
        Assert.Equal(800001U, reader.ReadUInt32(12));
        Assert.Equal(3U, reader.ReadUInt32(16));
        Assert.Equal(0U, reader.ReadUInt32(20));
        Assert.Equal(0U, reader.ReadUInt32(24));
        Assert.Equal(0, reader.ReadInt32(28));
        Assert.Equal(0U, reader.ReadUInt32(32));
        Assert.Equal(1002U, reader.ReadUInt32(36));
        Assert.Equal(LegacyItemExtraPacket.PermanentRefineryType, reader.ReadUInt32(40));
        Assert.Equal(301U, reader.ReadUInt32(44));
        Assert.Equal(2U, reader.ReadUInt32(48));
        Assert.Equal(10U, reader.ReadUInt32(52));
        Assert.Equal(20U, reader.ReadUInt32(56));
        Assert.Equal(0, reader.ReadInt32(60));
    }

    [Fact]
    public void LegacyItemExtraPacketRoundTrips()
    {
        var packet = new LegacyItemExtraPacket(
            [new LegacyItemExtraPurification(1001, LegacyItemExtraPacket.StabilizationType, 800001, 3, 0, 0, 0)],
            [new LegacyItemExtraRefinery(1001, LegacyItemExtraPacket.PermanentRefineryType, 301, 2, 10, 20, 0)]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyItemExtraPacket.TryParse(frame, out var parsed));
        Assert.Single(parsed.Purifications);
        Assert.Single(parsed.Refineries);
        Assert.Equal(packet.Purifications[0], parsed.Purifications[0]);
        Assert.Equal(packet.Refineries[0], parsed.Refineries[0]);
    }

    [Fact]
    public void LegacyItemExtraTryCreateRejectsExpiredDurations()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1002,
            PurificationItemId = 800001,
            PurificationDuration = 60,
            PurificationAddedOn = new DateTime(2026, 6, 4, 12, 0, 0).ToBinary(),
        };

        Assert.False(
            LegacyItemExtraPacket.TryCreate(item, new DateTime(2026, 6, 4, 12, 2, 0), out _));
    }

    [Fact]
    public void LegacyItemExtraTryCreateUsesActiveDurationSecondsLeft()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1002,
            RefineryEffectId = 3005000,
            RefineryEffectDuration = 120,
            RefineryEffectAddedOn = new DateTime(2026, 6, 4, 12, 0, 0).ToBinary(),
        };

        Assert.True(
            LegacyItemExtraPacket.TryCreate(item, new DateTime(2026, 6, 4, 12, 1, 0), out var packet));
        var refinery = Assert.Single(packet.Refineries);
        Assert.Equal(LegacyItemExtraPacket.RefinaryAddingType, refinery.Type);
        Assert.Equal(60, refinery.SecondsLeft);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
