using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySourceSilentPacketTests
{
    [Theory]
    [InlineData("0a00ed07080720012806", LegacySourceSilentPacket.LegacyClientOpcode2029, "080720012806")]
    [InlineData("06009408080b", LegacySourceSilentPacket.LegacyClientOpcode2196, "080b")]
    [InlineData("09004f090800108f02", LegacySourceSilentPacket.LegacyClientOpcode2383, "0800108f02")]
    [InlineData("0800810908031000", LegacySourceSilentPacket.LegacyClientOpcode2433, "08031000")]
    [InlineData("0800810908051000", LegacySourceSilentPacket.LegacyClientOpcode2433, "08051000")]
    [InlineData("0800810908081000", LegacySourceSilentPacket.LegacyClientOpcode2433, "08081000")]
    public void LegacySourceSilentCapturedFrameParsesAndRoundTripsExactly(
        string frameHex,
        ushort opcode,
        string payloadHex)
    {
        var frameBytes = Convert.FromHexString(frameHex);
        var frame = ReadFrame(frameBytes);

        Assert.True(LegacySourceSilentPacket.TryParse(frame, out var packet));

        Assert.Equal(opcode, packet.Opcode);
        Assert.Equal(Convert.FromHexString(payloadHex), packet.Payload.ToArray());
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySourceSilentLive2369LoggedFrameShapeRoundTripsWithCapturedPrefix()
    {
        var capturedPrefix = Convert.FromHexString(
            "0800120208031202080412020807120208091202080C1202080F120208101202");
        var writer = new PacketWriter(LegacySourceSilentPacket.LegacyClientOpcode2369, payloadLength: 74);
        capturedPrefix.AsSpan().CopyTo(writer.Payload);
        var frameBytes = writer.ToArray();

        Assert.True(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out var packet));

        Assert.Equal(LegacySourceSilentPacket.LegacyClientOpcode2369, packet.Opcode);
        Assert.Equal(74, packet.Payload.Length);
        Assert.True(capturedPrefix.AsSpan().SequenceEqual(packet.Payload.Span[..capturedPrefix.Length]));
        Assert.Equal(frameBytes, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySourceSilentRejectsAchievementBecauseNativeInboundHandlerExists()
    {
        var capturedPayload = Convert.FromHexString("020000005AC81300000000007429000000000000");
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyAchievement, capturedPayload.Length);
        capturedPayload.CopyTo(writer.Payload);
        var frameBytes = writer.ToArray();

        Assert.False(LegacySourceSilentPacket.TryParse(ReadFrame(frameBytes), out _));
        Assert.False(LegacySourceSilentPacket.IsSourceSilentOpcode(WorldPacketOpcodes.LegacyAchievement));
        Assert.False(LegacySourceSilentPacket.TryGetOpcodeInfo(WorldPacketOpcodes.LegacyAchievement, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacySourceSilentPacket.GetOpcodeInfo(WorldPacketOpcodes.LegacyAchievement));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LegacySourceSilentPacket(WorldPacketOpcodes.LegacyAchievement, capturedPayload));
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
        LegacySourceSilentPacket.LegacyClientOpcode2311,
        "opcode 2311 has no [Packet(2311)] inbound handler",
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
