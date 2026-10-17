using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAchievementPacketTests
{
    [Fact]
    public void LegacyAchievementOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2307, WorldPacketOpcodes.LegacyAchievement);
        Assert.Equal(WorldPacketOpcodes.LegacyAchievement, LegacyAchievementPacket.Opcode);
    }

    [Fact]
    public void LegacyAchievementStartupPacketWritesLegacyBigHashShape()
    {
        var flags = new uint[LegacyAchievementState.FlagWordCount];
        flags[0] = 171_966_688;
        flags[13] = 42;
        var packet = LegacyAchievementPacket.Startup(1_293_600, new LegacyAchievementState(flags));
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyAchievement, frame.Header.Opcode);
        Assert.Equal(LegacyAchievementPacket.BigHashPayloadLength, frame.Payload.Length);
        Assert.True(LegacyAchievementPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyAchievementPacket.BigHashAction, parsed.Action);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(flags, parsed.Flags);
    }

    [Fact]
    public void LegacyAchievementStateParsesMisspelledLegacySnapshotValue()
    {
        var state = LegacyAchievementState.Parse("0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/");

        Assert.Equal(LegacyAchievementState.FlagWordCount, state.Flags.Count);
        Assert.Equal(171_966_688U, state.Flags[0]);
        Assert.Equal(262_144U, state.Flags[1]);
        Assert.Equal(6_817_280U, state.Flags[12]);
        Assert.Equal(0U, state.Flags[13]);
    }

    [Fact]
    public void LegacyAchievementStateUsesZeroFlagsForMissingSnapshot()
    {
        var state = LegacyAchievementState.Parse("");

        Assert.Equal(LegacyAchievementState.FlagWordCount, state.Flags.Count);
        Assert.All(state.Flags, flag => Assert.Equal(0U, flag));
    }

    [Fact]
    public void LegacyAchievementPacketRejectsWrongCountForBigHashPayload()
    {
        var writer = new PacketWriter(LegacyAchievementPacket.Opcode, payloadLength: 16);
        writer.WriteUInt32(0, LegacyAchievementPacket.BigHashAction);
        writer.WriteUInt32(4, 1_293_600);
        writer.WriteUInt32(8, 0);
        writer.WriteUInt32(12, LegacyAchievementState.FlagWordCount);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyAchievementPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
