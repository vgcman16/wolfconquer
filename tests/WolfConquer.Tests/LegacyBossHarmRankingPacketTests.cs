using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossHarmRankingPacketTests
{
    [Fact]
    public void LegacyBossHarmRankingOpcodeMatchesSourceFinalize()
    {
        Assert.Equal((ushort)2001, LegacyBossHarmRankingPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyBossHarmRanking, LegacyBossHarmRankingPacket.Opcode);
    }

    [Fact]
    public void LegacyBossHarmRankingWritesSourceProtobufShape()
    {
        var packet = new LegacyBossHarmRankingPacket(
            LegacyBossHarmRankingAction.ShowRespondForTheRest,
            MonsterId: 4171,
            [
                new LegacyBossHarmRankingPacketEntry(
                    Rank: 1,
                    ServerId: 2,
                    HunterUid: 1_293_600,
                    HunterName: "TestSrv",
                    HunterScore: 250),
            ]);

        byte[] expected =
        [
            0x1F, 0x00, 0xD1, 0x07,
            0x08, 0x02,
            0x10, 0xCB, 0x20,
            0x1A, 0x14,
            0x08, 0x01,
            0x10, 0x02,
            0x18, 0xA0, 0xFA, 0x4E,
            0x22, 0x07, (byte)'T', (byte)'e', (byte)'s', (byte)'t', (byte)'S', (byte)'r', (byte)'v',
            0x28, 0xFA, 0x01,
        ];
        Assert.Equal(expected, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyBossHarmRankingRoundTripsMultipleHunters()
    {
        var packet = new LegacyBossHarmRankingPacket(
            LegacyBossHarmRankingAction.ShowRespondForTheRest,
            MonsterId: 4171,
            [
                new LegacyBossHarmRankingPacketEntry(1, 2, 1_293_600, "TestSrv", 250),
                new LegacyBossHarmRankingPacketEntry(2, 2, 1_293_601, "Ninja", 200),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyBossHarmRankingPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.MonsterId, parsed.MonsterId);
        Assert.Equal(packet.Hunters, parsed.Hunters);
    }

    [Fact]
    public void LegacyBossHarmRankingCreatesShowForTheRestFromRuntimeRanking()
    {
        var packet = LegacyBossHarmRankingPacket.CreateShowForTheRest(
            monsterId: 4171,
            [
                new LegacyBossHarmRankingEntry(1_001, "TestSrv", 250, 1),
                new LegacyBossHarmRankingEntry(1_002, "Ninja", 200, 2),
            ],
            serverId: 2);

        Assert.Equal(LegacyBossHarmRankingAction.ShowRespondForTheRest, packet.Type);
        Assert.Equal(4171U, packet.MonsterId);
        Assert.Collection(
            packet.Hunters,
            first =>
            {
                Assert.Equal(1U, first.Rank);
                Assert.Equal(2U, first.ServerId);
                Assert.Equal(1_001U, first.HunterUid);
                Assert.Equal("TestSrv", first.HunterName);
                Assert.Equal(250U, first.HunterScore);
            },
            second =>
            {
                Assert.Equal(2U, second.Rank);
                Assert.Equal(2U, second.ServerId);
                Assert.Equal(1_002U, second.HunterUid);
                Assert.Equal("Ninja", second.HunterName);
                Assert.Equal(200U, second.HunterScore);
            });
    }

    [Fact]
    public void LegacyBossHarmRankingRejectsMissingRequiredFields()
    {
        var writer = new PacketWriter(LegacyBossHarmRankingPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x02;

        Assert.False(LegacyBossHarmRankingPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
