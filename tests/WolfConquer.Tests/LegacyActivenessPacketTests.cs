using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyActivenessPacketTests
{
    [Fact]
    public void LegacyActivenessOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2316, WorldPacketOpcodes.LegacyActivityTasks);
        Assert.Equal((ushort)2174, WorldPacketOpcodes.LegacyActivityRewardsInfo);
        Assert.Equal((ushort)2105, WorldPacketOpcodes.LegacyActivityClaims);
    }

    [Fact]
    public void LegacyActivityTasksPacketWritesLegacyTaskListShape()
    {
        var packet = new LegacyActivityTasksPacket(
            LegacyActivityTasksPacket.InitializeListAction,
            [
                new LegacyActivityTaskState(1, Progress: 10, Completed: 1),
                new LegacyActivityTaskState(22, Progress: 0, Completed: 0),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyActivityTasks, frame.Header.Opcode);
        Assert.Equal(
        [
            0x03,
            0x02,
            0x01, 0x00, 0x00, 0x00,
            0x01,
            0x0A,
            0x16, 0x00, 0x00, 0x00,
            0x00,
            0x00,
        ], frame.Payload.ToArray());
        Assert.True(LegacyActivityTasksPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Action, parsed.Action);
        Assert.Equal(packet.Tasks, parsed.Tasks);
    }

    [Fact]
    public void LegacyActivityRewardsInfoPacketWritesLegacyFixedShape()
    {
        var frame = ReadFrame(new LegacyActivityRewardsInfoPacket(
            LegacyActivityRewardsInfoPacket.InitializeListAction,
            ActivityPoints: 80).ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyActivityRewardsInfo, frame.Header.Opcode);
        Assert.True(LegacyActivityRewardsInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyActivityRewardsInfoPacket.InitializeListAction, parsed.Action);
        Assert.Equal(80U, parsed.ActivityPoints);
    }

    [Fact]
    public void LegacyActivityClaimsPacketWritesClaimRewardByte()
    {
        var frame = ReadFrame(new LegacyActivityClaimsPacket(ClaimRewards: 2).ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyActivityClaims, frame.Header.Opcode);
        Assert.Equal([0x02], frame.Payload.ToArray());
        Assert.True(LegacyActivityClaimsPacket.TryParse(frame, out var parsed));
        Assert.Equal(2, parsed.ClaimRewards);
    }

    [Fact]
    public void LegacyActivenessStateParsesLegacySlashDelimitedSnapshot()
    {
        var state = LegacyActivenessState.Parse("2/80/2/1/10/1/22/0/0/");

        Assert.Equal(2, state.ClaimRewards);
        Assert.Equal(80U, state.ActivityPoints);
        Assert.Equal(
        [
            new LegacyActivityTaskState(1, Progress: 10, Completed: 1),
            new LegacyActivityTaskState(22, Progress: 0, Completed: 0),
        ], state.Tasks);
    }

    [Fact]
    public void LegacyActivenessStateUsesLegacyDefaultsForMissingSnapshot()
    {
        var state = LegacyActivenessState.Parse("");

        Assert.Equal(0, state.ClaimRewards);
        Assert.Equal(0U, state.ActivityPoints);
        Assert.Empty(state.Tasks);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
