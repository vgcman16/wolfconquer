using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemInfoEnvelopeTests
{
    private static PacketFrame Frame(byte[] payload, ushort opcode = 2350)
    {
        var writer = new PacketWriter(opcode, payload.Length, ushort.MaxValue);
        payload.CopyTo(writer.Payload);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(writer.ToArray()), out var frame, out _, out _, ushort.MaxValue));
        return frame;
    }

    [Fact]
    public void ActualDecoderFrameRoundtripsAndDoesNotRegisterAHandler()
    {
        var frame = Frame(Convert.FromHexString(Extended));
        Assert.True(Chinese7937ItemInfoEnvelope.TryParseFrame(frame, 7937, out var parsed, out var failure), failure.ToString());
        var envelope = Assert.IsType<Chinese7937ItemInfoEnvelope>(parsed);
        Assert.Equal(frame.Frame.ToArray(), envelope.ToFrameBytes());
        var copy = envelope.ToFrameBytes(); Array.Clear(copy);
        Assert.Equal(frame.Frame.ToArray(), envelope.ToFrameBytes());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        Assert.False(registry.TryGetHandler(2350, out _));
    }

    [Fact]
    public void HeaderFrameAndPayloadMustAllDescribeTheSameActualMessage()
    {
        var frame = Frame(Convert.FromHexString(Extended));
        var wrongPayload = frame.Payload.ToArray(); wrongPayload[0] ^= 1;
        var cases = new[]
        {
            frame with { Header = new PacketHeader((ushort)(frame.Header.Length - 1), 2350) },
            frame with { Header = new PacketHeader(frame.Header.Length, 1008) },
            frame with { Payload = new ReadOnlySequence<byte>(wrongPayload) },
            frame with { Payload = ReadOnlySequence<byte>.Empty },
            frame with { Frame = ReadOnlySequence<byte>.Empty },
            Frame(Convert.FromHexString(Required), 1008),
        };
        foreach (var malformed in cases)
        {
            Assert.False(Chinese7937ItemInfoEnvelope.TryParseFrame(malformed, 7937, out var envelope, out var failure));
            Assert.Null(envelope);
            Assert.Equal(Chinese7937ItemInfoEnvelopeFailure.MalformedFrame, failure);
        }
    }

    [Fact]
    public void AcceptedMaximumPayloadHasAnExplicitFullUInt16FrameWriterBound()
    {
        // Six required bytes, two-byte unknown tag44/2, three-byte length65520.
        var prefix = Convert.FromHexString(Required + "E202F0FF03");
        var payload = new byte[Chinese7937ItemInfoEnvelope.MaximumPayloadBytes];
        prefix.CopyTo(payload, 0);
        Assert.Equal(65520, payload.Length - prefix.Length);
        Assert.True(Chinese7937ItemInfoEnvelope.TryParsePayload(payload, 7937, out var parsed, out var failure), failure.ToString());
        var envelope = Assert.IsType<Chinese7937ItemInfoEnvelope>(parsed);
        Assert.Equal((int)ushort.MaxValue, envelope.ToFrameBytes().Length);
        var frame = Frame(payload);
        Assert.True(Chinese7937ItemInfoEnvelope.TryParseFrame(frame, 7937, out var framed, out failure), failure.ToString());
        Assert.Equal(payload, framed!.ToPayloadBytes());
    }
}
