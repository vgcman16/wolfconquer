using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCaptureTheFlagPacketTests
{
    [Fact]
    public void LegacyCaptureTheFlagUpdateOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2062, WorldPacketOpcodes.LegacyCaptureTheFlagUpdate);
        Assert.Equal(WorldPacketOpcodes.LegacyCaptureTheFlagUpdate, LegacyCaptureTheFlagUpdatePacket.Opcode);
    }

    [Fact]
    public void LegacyCaptureTheFlagRemoveFlagEffectRoundTripsSourceShape()
    {
        var packet = LegacyCaptureTheFlagUpdatePacket.RemoveFlagEffect(1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyCaptureTheFlagUpdate, frame.Header.Opcode);
        Assert.Equal(LegacyCaptureTheFlagUpdatePacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyCaptureTheFlagUpdatePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect, parsed.Mode);
        Assert.Equal(1_293_600U, parsed.Parameter);
        Assert.Equal(0, parsed.Parameter2);
        Assert.Equal(1, parsed.Count);
    }

    [Fact]
    public void LegacyCaptureTheFlagUpdatePointRoundTripsSourceShape()
    {
        var packet = LegacyCaptureTheFlagRankingPacket.UpdatePoint(27);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyCaptureTheFlagRankingPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyCaptureTheFlagRankingPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyCaptureTheFlagRankingPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyCaptureTheFlagRankingAction.UpdatePoint, parsed.Action);
        Assert.Equal(1U, parsed.Page);
        Assert.Equal(1, parsed.Marker);
        Assert.Equal(27U, parsed.Parameter3);
        Assert.Equal(0UL, parsed.Parameter4);
    }

    [Fact]
    public void LegacyCaptureTheFlagScoreBaseUsesSourceEntryRows()
    {
        var bytes = LegacyCaptureTheFlagUpdatePacket.ScoreBase(
            [
                new LegacyCaptureTheFlagScoreEntry(0, 900, "SourceGuild"),
                new LegacyCaptureTheFlagScoreEntry(1, 300, "OtherGuild"),
                new LegacyCaptureTheFlagScoreEntry(2, 200, "ThirdGuild"),
                new LegacyCaptureTheFlagScoreEntry(3, 100, "FourthGuild"),
                new LegacyCaptureTheFlagScoreEntry(4, 50, "FifthGuild"),
                new LegacyCaptureTheFlagScoreEntry(5, 25, "IgnoredGuild"),
            ]);
        var frame = ReadFrame(bytes);

        Assert.Equal(WorldPacketOpcodes.LegacyCaptureTheFlagUpdate, frame.Header.Opcode);
        Assert.Equal(
            LegacyCaptureTheFlagUpdatePacket.PayloadLength + 5 * LegacyCaptureTheFlagUpdatePacket.ScoreEntryLength,
            frame.Payload.Length);
        Assert.True(PacketReader.TryRead(frame.Frame.ToArray(), out var reader));
        Assert.Equal((uint)LegacyCaptureTheFlagUpdateMode.ScoreBase, reader.ReadUInt32(0));
        Assert.Equal(0U, reader.ReadUInt32(4));
        Assert.Equal(5, reader.ReadUInt16(24));
        Assert.Equal(1, reader.ReadUInt16(26));

        Assert.Equal(0U, reader.ReadUInt32(28));
        Assert.Equal(900U, reader.ReadUInt32(32));
        Assert.Equal(0U, reader.ReadUInt32(36));
        Assert.Equal("SourceGuild", reader.ReadCString(40, 32, Encoding.ASCII));

        var fifthOffset = LegacyCaptureTheFlagUpdatePacket.PayloadLength +
            4 * LegacyCaptureTheFlagUpdatePacket.ScoreEntryLength;
        Assert.Equal(4U, reader.ReadUInt32(fifthOffset));
        Assert.Equal(50U, reader.ReadUInt32(fifthOffset + 4));
        Assert.Equal("FifthGuild", reader.ReadCString(fifthOffset + 12, 32, Encoding.ASCII));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
