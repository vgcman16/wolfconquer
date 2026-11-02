using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoOperationEnvelopeTests
{
    [Fact]
    public void IndependentFrameGoldenReplaysExactExtensionsWithoutRegisteringAHandler()
    {
        // length12, opcode2132; child9 length2 contains opaque field3, then op8.
        var original = Convert.FromHexString("0C00540858004A0218000808");
        var frame = Decode(original);
        Assert.True(Chinese7937EchoOperationEnvelope.TryParseFrame(frame, 7937, out var parsed, out var failure), failure.ToString());
        var envelope = Assert.IsType<Chinese7937EchoOperationEnvelope>(parsed);
        Assert.Equal(Chinese7937EchoEnvelopeFailure.None, failure);
        Assert.Equal(original, envelope.ToFrameBytes());
        Assert.Equal(Convert.FromHexString("58004A0218000808"), envelope.ToPayloadBytes());
        var copy = envelope.ToFrameBytes(); Array.Clear(copy);
        Assert.Equal(original, envelope.ToFrameBytes());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        Assert.False(registry.TryGetHandler(2132, out _));
    }

    [Fact]
    public void ActualSegmentedDecoderAndSourceReuseKeepExactOwnedPayload()
    {
        var original = Convert.FromHexString("0C00540858004A0218000808");
        var source = original.ToArray();
        var first = new ByteSegment(source.AsMemory(0, 1));
        var last = first;
        for (var index = 1; index < source.Length; index++) last = last.Append(source.AsMemory(index, 1));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        Assert.False(frame.Frame.IsSingleSegment); Assert.False(frame.Payload.IsSingleSegment);
        Assert.True(Chinese7937EchoOperationEnvelope.TryParseFrame(frame, 7937, out var parsed, out var failure), failure.ToString());
        Array.Clear(source);
        Assert.Equal(original, Assert.IsType<Chinese7937EchoOperationEnvelope>(parsed).ToFrameBytes());
    }

    [Fact]
    public void ActualHeaderWholeFrameAndSeparatePayloadMustAllAgree()
    {
        var frame = Decode(Convert.FromHexString("0C00540858004A0218000808"));
        var wrongPayload = frame.Payload.ToArray(); wrongPayload[0] ^= 1;
        var wrongHeader = frame.Frame.ToArray(); wrongHeader[0]--;
        var cases = new[]
        {
            frame with { Header = new PacketHeader((ushort)(frame.Header.Length - 1), 2132) },
            frame with { Header = new PacketHeader(frame.Header.Length, 2350) },
            frame with { Frame = new ReadOnlySequence<byte>(wrongHeader) },
            frame with { Payload = new ReadOnlySequence<byte>(wrongPayload) },
            frame with { Payload = ReadOnlySequence<byte>.Empty },
            frame with { Frame = ReadOnlySequence<byte>.Empty },
            Decode(Convert.FromHexString("06002E090808")),
            new PacketFrame(new PacketHeader(0, 2132), new ReadOnlySequence<byte>(new byte[65536]),
                new ReadOnlySequence<byte>(new byte[65532])),
        };
        foreach (var malformed in cases)
        {
            Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(malformed, 7937, out var result, out var failure));
            Assert.Null(result); Assert.Equal(Chinese7937EchoEnvelopeFailure.MalformedFrame, failure);
        }
    }

    [Fact]
    public void CoherentFrameStillRequiresSupportedVersionAndCompleteValidPayload()
    {
        var frame = Decode(Convert.FromHexString("060054080808"));
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(frame, 7935, out var parsed, out var failure));
        Assert.Null(parsed); Assert.Equal(Chinese7937EchoEnvelopeFailure.UnsupportedVersion, failure);
        frame = Decode(Convert.FromHexString("07005408080858"));
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(frame, 7937, out parsed, out failure));
        Assert.Null(parsed); Assert.Equal(Chinese7937EchoEnvelopeFailure.InvalidWire, failure);
        frame = Decode(Convert.FromHexString("04005408"));
        Assert.False(Chinese7937EchoOperationEnvelope.TryParseFrame(frame, 7937, out parsed, out failure));
        Assert.Null(parsed); Assert.Equal(Chinese7937EchoEnvelopeFailure.InputBounds, failure);
    }

    private static PacketFrame Decode(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _, ushort.MaxValue));
        return frame;
    }

    private sealed class ByteSegment : ReadOnlySequenceSegment<byte>
    {
        internal ByteSegment(ReadOnlyMemory<byte> bytes) => Memory = bytes;

        internal ByteSegment Append(ReadOnlyMemory<byte> bytes)
        {
            var next = new ByteSegment(bytes) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
