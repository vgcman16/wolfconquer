using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySourceSilentPacketTests
{
    [Theory]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2029, 6, 1)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2196, 2, 2)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2383, 5, 3)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2433, 4, 4)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2433, 4, 5)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2433, 4, 6)]
    public void LegacySourceSilentSyntheticFrameParsesAndRoundTripsExactly(
        ushort opcode, int payloadLength, int seed)
    {
        var payload = Enumerable.Range(0, payloadLength).Select(index => (byte)(seed + index * 17)).ToArray();
        var frameBytes = new byte[4 + payloadLength];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frameBytes.AsSpan(0), (ushort)frameBytes.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frameBytes.AsSpan(2), opcode);
        payload.CopyTo(frameBytes, 4);
        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));
        Assert.Equal(opcode, packet.Opcode);
        Assert.Equal(payload, packet.Payload.ToArray());
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySourceSilent2369RoundTripsSyntheticPrefixAndZeroSuffix()
    {
        var syntheticPrefix = Enumerable.Range(1, 53).Select(index => (byte)(index * 3)).ToArray();
        var writer = new PacketWriter(LegacySourceSilentPacket.LegacyClientOpcode2369, payloadLength: 74);
        syntheticPrefix.AsSpan().CopyTo(writer.Payload);
        var frameBytes = writer.ToArray();
        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));
        Assert.Equal(LegacySourceSilentPacket.LegacyClientOpcode2369, packet.Opcode);
        Assert.Equal(74, packet.Payload.Length);
        Assert.True(syntheticPrefix.AsSpan().SequenceEqual(packet.Payload.Span[..syntheticPrefix.Length]));
        Assert.All(packet.Payload.Span[syntheticPrefix.Length..].ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySourceSilentAchievementRequestRoundTripsSyntheticPayload()
    {
        var writer = new PacketWriter(LegacySourceSilentPacket.LegacyClientOpcode2307, 20);
        writer.WriteUInt32(0, 2);
        writer.WriteUInt32(4, 12_001);
        writer.WriteUInt32(12, 77);
        var frameBytes = writer.ToArray();
        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));
        Assert.Equal(LegacySourceSilentPacket.LegacyClientOpcode2307, packet.Opcode);
        Assert.Equal(writer.Payload.ToArray(), packet.Payload.ToArray());
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySourceSilentLive2311StartupFrameRoundTripsWithZeroPayload()
    {
        var writer = new PacketWriter(LegacySourceSilentPacket.LegacyClientOpcode2311, payloadLength: 516);
        var frameBytes = writer.ToArray();

        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));

        Assert.Equal(LegacySourceSilentPacket.LegacyClientOpcode2311, packet.Opcode);
        Assert.Equal(516, packet.Payload.Length);
        Assert.All(packet.Payload.ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Theory]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2029)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2038)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2145)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2163)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2234)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2307)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2311)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2357)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2369)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2382)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2417)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2424)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2433)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2456)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2469)]
    public void LegacySourceSilentLiveOpcodeRoundTripsRawPayload(ushort opcode)
    {
        var writer = new PacketWriter(opcode, payloadLength: 3);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;
        writer.Payload[2] = 0xCC;
        var frameBytes = writer.ToArray();

        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));

        Assert.Equal(opcode, packet.Opcode);
        Assert.Equal(Convert.FromHexString("AABBCC"), packet.Payload.ToArray());
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Theory]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2029, "CMsgRedEnvelops")]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2038, "CMsgTaskReward")]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2145, "CMsgAuctionItem")]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2163, "CMsgPing")]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2196, "CMsgTexasExInteractive")]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2234, "CMsgExpPool")]
    public void LegacySourceSilentCatalogNamedOpcodeCarriesSourceEvidence(ushort opcode, string sourceName)
    {
        var info = LegacySourceSilentPacket.GetOpcodeInfo(opcode);

        Assert.Equal(opcode, info.Opcode);
        Assert.Equal(sourceName, info.SourceName);
        Assert.Contains("MrRayzo.Packets catalog", info.SourceEvidence);
        Assert.Contains("no [Packet] attribute", info.SourceEvidence);
    }

    [Theory]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2357)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2311)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2382)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2383)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2417)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2424)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2456)]
    [InlineData(LegacySourceSilentPacket.LegacyClientOpcode2469)]
    public void LegacySourceSilentUncatalogedOpcodeCarriesUnmappedSourceEvidence(ushort opcode)
    {
        var info = LegacySourceSilentPacket.GetOpcodeInfo(opcode);

        Assert.Equal(opcode, info.Opcode);
        Assert.Null(info.SourceName);
        Assert.Contains("no [Packet] attribute", info.SourceEvidence);
        Assert.Contains("MrRayzo.Packets/GamePackets catalog name", info.SourceEvidence);
    }

    [Theory]
    [InlineData(
        LegacySourceSilentPacket.LegacyClientOpcode2369,
        "opcode 2369 is absent from MrRayzo.Game.GamePackets",
        "captured 4 source-silent frames with payloadLength=74")]
    [InlineData(
        LegacySourceSilentPacket.LegacyClientOpcode2433,
        "opcode 2433 is absent from MrRayzo.Game.GamePackets",
        "captured 9 source-silent frames with payloadLength=4")]
    [InlineData(
        LegacySourceSilentPacket.LegacyClientOpcode2307,
        "opcode 2307 is the outbound LegacyAchievement startup packet",
        "captured compact 20-byte client frames")]
    [InlineData(
        LegacySourceSilentPacket.LegacyClientOpcode2311,
        "opcode 2311 has no [Packet(2311)] attribute",
        "observed an all-zero 516-byte client frame")]
    public void LegacySourceSilentLaneOpcodesCarrySpecificSourceAndLiveEvidence(
        ushort opcode,
        string sourceEvidence,
        string liveEvidence)
    {
        var info = LegacySourceSilentPacket.GetOpcodeInfo(opcode);

        Assert.Equal(opcode, info.Opcode);
        Assert.Contains(sourceEvidence, info.SourceEvidence);
        Assert.Contains("Program.Game_Receive only dispatches registered Pool.MsgInvoker handlers", info.SourceEvidence);
        Assert.Contains(liveEvidence, info.SourceEvidence);
    }

    [Fact]
    public void LegacySourceSilentRejectsLegacyInstanceOpcode2299()
    {
        var frameBytes = new LegacyInstancePacket(LegacyInstanceAction.Info).ToFrameBytes();

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out _));
    }

    [Fact]
    public void LegacySourceSilentRejectsLegacyLotteryListOpcode2432()
    {
        var frameBytes = LegacyLotteryListPacket.Query().ToFrameBytes();

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out _));
    }

    [Fact]
    public void LegacySourceSilentRejectsLegacyCaptureTheFlagUpdateOpcode2062()
    {
        var frameBytes = new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.InitializeRank,
            Parameter: 0,
            Parameter2: 0,
            Count: 0).ToFrameBytes();

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out _));
    }

    [Fact]
    public void LegacySourceSilentRejectsLegacyTeamElitePkBracketsOpcode2198()
    {
        var frameBytes = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.RequestInformation,
            Page: 1,
            PacketNumber: 0,
            MatchCount: 0,
            Group: 2,
            LegacyTeamElitePkBracketGuiType.Top8Ranking,
            TimeLeft: 0,
            PacketNumber2: 0,
            TotalMatches: 0).ToFrameBytes();

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out _));
    }

    [Fact]
    public void LegacySourceSilentRejectsNonSourceSilentOpcode()
    {
        var writer = new PacketWriter(1000, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x0B;

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
