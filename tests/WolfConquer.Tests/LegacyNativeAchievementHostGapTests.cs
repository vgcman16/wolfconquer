using System.Buffers;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeAchievementHostGapTests
{
    [Fact]
    public async Task CapturedAchievementRequestReportsKnownNativeReceiverButHeldHostAdoptionWithoutReply()
    {
        byte[] payload = Convert.FromHexString("020000005AC81300000000007429000000000000");
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyAchievement, payload.Length); payload.CopyTo(writer.Payload);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(writer.ToArray()), out var frame, out _, out _));
        using var log = new StringWriter(); var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, null, (bytes, _) => { sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; });
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        await handler.HandleFrameAsync(connection, frame);
        Assert.Empty(sent); Assert.Null(connection.NativeGameClient);
        var output = log.ToString();
        Assert.Contains("unsupported-packet", output); Assert.Contains("packet=ClientAchievement", output);
        Assert.Contains("native ClientAchievement.AchievementHandler [Packet(2307)] exists", output);
        Assert.Contains("achievement persistence/load and host owner adoption remain unintegrated", output);
        Assert.Contains("PARITY-GAP native achievement host adoption", output);
        Assert.Contains("opcode=2307 payloadLength=20 payloadSampleHex=020000005AC81300000000007429000000000000", output);
        Assert.DoesNotContain("source-silent", output); Assert.DoesNotContain("unknown world opcode", output);
        Assert.DoesNotContain("handler exception", output);
    }

    [Fact]
    public void Opcode2311EvidenceAcknowledgesUnsentTtpSerializerWithoutInventingInboundHandler()
    {
        Assert.True(LegacySourceSilentPacket.TryGetOpcodeInfo(2311, out var info));
        Assert.Contains("no [Packet(2311)] inbound handler", info.SourceEvidence);
        Assert.Contains("MsgMessage.cs command ttp writes 21 literal payload bytes and calls Finalize(2311)", info.SourceEvidence);
        Assert.Contains("then GetLeagueMainRank, without sending that packet", info.SourceEvidence);
        Assert.Contains("this serializer does not establish an inbound handler", info.SourceEvidence);
        Assert.DoesNotContain("no Finalize(2311)", info.SourceEvidence);
    }
}
