using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposeRequestTests
{
    [Theory]
    [InlineData(0U)] [InlineData(50U)] [InlineData(uint.MaxValue)]
    public void InvalidDeclaredCountsRefuseWithoutAnEmptySuccess(uint declared) =>
        Refuses(BuildFrame(declared, [7]), "relicDecomposeCountInvalid");

    [Theory]
    [InlineData(0U, 7U)] [InlineData(7U, 0U)] [InlineData(7U, 7U)]
    public void ZeroAndDuplicateDeclaredUidsRefuseTheWholeBatch(uint first, uint second) =>
        Refuses(BuildFrame(2, [first, second]), "relicDecomposeDistinctInputsRequired");

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ACompleteDeclaredPrefixCannotHideAPartialTrailingWord(int partial) =>
        Refuses(BuildFrame(1, [7], partial), "relicDecomposeBodyInvalid");

    [Fact]
    public void MissingDeclaredWordsAndShortBasePayloadNeverReadPastInput()
    {
        Refuses(BuildFrame(2, [7]), "relicDecomposeBodyInvalid");
        var writer = new PacketWriter(2137, 30);
        writer.WriteUInt16(12, 67); writer.WriteUInt32(18, 1);
        Refuses(writer.ToArray(), "relicDecomposeBodyInvalid");
    }

    [Fact]
    public void PacketAndFrameComponentsMustAgreeWithTheActualActionAndHeader()
    {
        var (packet, frame) = ParseFrame(BuildFrame(1, [7]));
        Assert.False(LegacyRelicDecomposeRequest.TryParse(packet with { Timestamp = 456 }, frame, out var request, out var reason));
        Assert.Null(request); Assert.Equal("relicDecomposePacketMismatch", reason);
        var changedPayload = frame.Payload.ToArray(); changedPayload[^1] ^= 1;
        PacketFrame[] invalid =
        [
            default,
            frame with { Header = new(frame.Header.Length, 2138) },
            frame with { Header = new((ushort)(frame.Header.Length - 1), frame.Header.Opcode) },
            frame with { Payload = new ReadOnlySequence<byte>(changedPayload) },
            frame with { Frame = frame.Frame.Slice(0, frame.Frame.Length - 1) },
            frame with { Frame = new ReadOnlySequence<byte>(new byte[65_536]) },
        ];
        foreach (var forged in invalid)
        {
            Assert.False(LegacyRelicDecomposeRequest.TryParse(packet, forged, out request, out reason));
            Assert.Null(request); Assert.Equal("relicDecomposeFrameInvalid", reason);
        }
        var other = new MsgItemUsagePacket(17, 0, MsgItemUsageAction.Ping, 0, 1, 0, 0);
        var writer = new PacketWriter(2137, 91);
        writer.WriteUInt32(0, 17); writer.WriteUInt16(12, (ushort)other.Action); writer.WriteUInt32(18, 1); writer.WriteUInt32(87, 7);
        var (_, wrongActionFrame) = ParseFrame(writer.ToArray());
        Assert.False(LegacyRelicDecomposeRequest.TryParse(other, wrongActionFrame, out request, out reason));
        Assert.Null(request); Assert.Equal("relicDecomposePacketMismatch", reason);
    }

    private static void Refuses(byte[] bytes, string expected)
    {
        var original = bytes.ToArray(); var (packet, frame) = ParseFrame(bytes);
        Assert.False(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason));
        Assert.Null(request); Assert.Equal(expected, reason); Assert.Equal(original, bytes);
    }
}
