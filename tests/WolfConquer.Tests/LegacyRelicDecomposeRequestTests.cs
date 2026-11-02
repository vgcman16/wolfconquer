using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposeRequestTests
{
    private const string GoldenFields = "11000000080706050403020143004433221101000000DDCCBBAA44332211";

    [Fact]
    public void IndependentWholeFrameGoldenKeepsHarmlessSurplusOnlyInOriginalCapture()
    {
        var bytes = Convert.FromHexString("63005908" + GoldenFields + new string('0', 114) + "07000000FFFFFFFF");
        var echo = Convert.FromHexString("5F005908" + GoldenFields + new string('0', 114) + "07000000");
        var (packet, frame) = ParseFrame(bytes);
        Assert.Equal(new MsgItemUsagePacket(17, 0x0102_0304_0506_0708, MsgItemUsageAction.DecomposeRelics,
            0x1122_3344, 1, 0xaabb_ccdd, 0x1122_3344), packet);
        Assert.True(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason), reason);
        Assert.NotNull(request);
        Assert.Equal(new uint[] { 7 }, request.SelectedUids.ToArray());
        Assert.Equal(bytes, request.CopyOriginalFrame());
        Assert.Equal(echo, request.CopyEchoFrame());
        Assert.Equal(packet, request.Packet);
    }

    [Fact]
    public void CopiesOfOriginalEchoAndSelectedWordsCannotMutateTheRequest()
    {
        var bytes = BuildFrame(2, [7, 8, 0, 7]);
        var original = bytes.ToArray();
        var (packet, frame) = ParseFrame(bytes);
        Assert.True(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason), reason);
        Assert.NotNull(request);
        var echo = request.CopyEchoFrame();
        Array.Clear(bytes); Array.Clear(request.CopyOriginalFrame()); Array.Clear(request.CopyEchoFrame());
        var words = request.SelectedUids.ToArray(); Array.Clear(words);
        Assert.Equal(original, request.CopyOriginalFrame());
        Assert.Equal(echo, request.CopyEchoFrame());
        Assert.Equal(new uint[] { 7, 8 }, request.SelectedUids.ToArray());
    }

    [Fact]
    public void ActualSegmentedFrameDecoderPreservesDeclaredOrderAndMaximumCount()
    {
        var ids = Enumerable.Range(1, 49).Select(value => (uint)value).ToArray();
        var bytes = BuildFrame(49, ids);
        var first = new Segment(bytes.AsMemory(0, 2));
        var last = first.Append(bytes.AsMemory(2, 31)).Append(bytes.AsMemory(33));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        Assert.False(frame.Frame.IsSingleSegment);
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var packet));
        Assert.True(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason), reason);
        Assert.NotNull(request); Assert.Equal(ids, request.SelectedUids.ToArray());
        Assert.Equal(bytes, request.CopyOriginalFrame()); Assert.Equal(bytes, request.CopyEchoFrame());
    }

    private static byte[] BuildFrame(uint declared, uint[] words, int partialBytes = 0)
    {
        var writer = new PacketWriter(2137, 87 + words.Length * 4 + partialBytes, ushort.MaxValue);
        writer.WriteUInt32(0, 17); writer.WriteUInt16(12, 67); writer.WriteUInt32(14, 123);
        writer.WriteUInt32(18, declared);
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(writer.Payload.Slice(87 + index * 4, 4), words[index]);
        return writer.ToArray();
    }

    private static (MsgItemUsagePacket Packet, PacketFrame Frame) ParseFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _, ushort.MaxValue));
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var packet));
        return (packet, frame);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        internal Segment(ReadOnlyMemory<byte> bytes) => Memory = bytes;
        internal Segment Append(ReadOnlyMemory<byte> bytes)
        {
            var next = new Segment(bytes) { RunningIndex = RunningIndex + Memory.Length };
            Next = next; return next;
        }
    }
}
