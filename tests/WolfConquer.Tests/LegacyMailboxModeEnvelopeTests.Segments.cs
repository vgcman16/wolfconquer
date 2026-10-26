using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxModeEnvelopeTests
{
    public enum EnvelopeDefect
    {
        OuterOpcode, OuterLength, EmbeddedOpcode, EmbeddedLength, MissingPhysical,
        ShortPhysical, TrailingPhysical, ShortPayload, TrailingPayload, DifferentPayload,
        PhysicalShortMetadata, PhysicalLongMetadata, PayloadShortMetadata, PayloadLongMetadata,
    }

    [Fact]
    public void IndependentBackingAndEveryPhysicalOrPayloadSplitPreserveFullWidthItems()
    {
        var packet = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, uint.MaxValue,
            [0, 0x0102030405060708UL, ulong.MaxValue]);
        var bytes = packet.ToFrameBytes();
        var frame = Frame(bytes);
        var payload = bytes[PacketHeader.Size..];
        for (var split = 1; split < bytes.Length; split++)
            AssertSegmentedAccepted(frame with { Frame = Split(bytes, split) }, packet);
        for (var split = 1; split < payload.Length; split++)
            AssertSegmentedAccepted(frame with { Payload = Split(payload, split) }, packet);
        AssertSegmentedAccepted(frame with { Frame = Split(bytes, 3), Payload = Split(payload, 1) }, packet);
    }

    [Theory]
    [InlineData(false, 4095, true)]
    [InlineData(false, 4096, false)]
    [InlineData(true, 4095, true)]
    [InlineData(true, 4096, false)]
    public void ExactSegmentVisitBudgetAppliesIndependentlyToPhysicalAndPayload(
        bool payloadSequence, int emptySegments, bool expected)
    {
        var original = new LegacyMailboxModePacket(LegacyMailboxModeType.DeleteClaim, 41, [ulong.MaxValue]);
        var bytes = original.ToFrameBytes();
        var frame = Frame(bytes);
        frame = payloadSequence
            ? frame with { Payload = WithEmptySegments(bytes[PacketHeader.Size..], emptySegments) }
            : frame with { Frame = WithEmptySegments(bytes, emptySegments) };
        if (expected) AssertSegmentedAccepted(frame, original);
        else AssertRejected(frame);
    }

    private static void AssertSegmentedAccepted(PacketFrame frame, LegacyMailboxModePacket expected)
    {
        Assert.True(LegacyMailboxModePacket.TryParse(frame, out var actual));
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Items, actual.Items);
    }

    private static PacketFrame Malformed(EnvelopeDefect defect)
    {
        var bytes = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 73, [ulong.MaxValue]).ToFrameBytes();
        var frame = Frame(bytes);
        var payload = bytes[PacketHeader.Size..];
        switch (defect)
        {
            case EnvelopeDefect.OuterOpcode: return frame with { Header = frame.Header with { Opcode = 1 } };
            case EnvelopeDefect.OuterLength: return frame with { Header = frame.Header with { Length = (ushort)(bytes.Length - 1) } };
            case EnvelopeDefect.EmbeddedOpcode: BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1); return frame;
            case EnvelopeDefect.EmbeddedLength: BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(bytes.Length - 1)); return frame;
            case EnvelopeDefect.MissingPhysical: return frame with { Frame = default };
            case EnvelopeDefect.ShortPhysical: return frame with { Frame = new ReadOnlySequence<byte>(bytes[..^1]) };
            case EnvelopeDefect.TrailingPhysical: return frame with { Frame = new ReadOnlySequence<byte>(AppendByte(bytes)) };
            case EnvelopeDefect.ShortPayload: return frame with { Payload = new ReadOnlySequence<byte>(payload[..^1]) };
            case EnvelopeDefect.TrailingPayload: return frame with { Payload = new ReadOnlySequence<byte>(AppendByte(payload)) };
            case EnvelopeDefect.DifferentPayload:
                payload[^1] ^= 0x7f;
                return frame with { Payload = new ReadOnlySequence<byte>(payload) };
            case EnvelopeDefect.PhysicalShortMetadata: return frame with { Frame = FalseLength(bytes, -1) };
            case EnvelopeDefect.PhysicalLongMetadata: return frame with { Frame = FalseLength(bytes, 1) };
            case EnvelopeDefect.PayloadShortMetadata: return frame with { Payload = FalseLength(payload, -1) };
            case EnvelopeDefect.PayloadLongMetadata: return frame with { Payload = FalseLength(payload, 1) };
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
    }

    private static ReadOnlySequence<byte> Split(byte[] bytes, int split)
    {
        var first = new Segment(bytes.AsMemory(0, split));
        var last = first.Append(bytes.AsMemory(split));
        return new(first, 0, last, last.Memory.Length);
    }

    private static byte[] AppendByte(byte[] bytes) => [.. bytes, 0x7f];

    private static ReadOnlySequence<byte> FalseLength(byte[] bytes, int physicalDelta)
    {
        var firstBytes = new byte[bytes.Length - 1 + physicalDelta];
        bytes.AsSpan(0, Math.Min(firstBytes.Length, bytes.Length)).CopyTo(firstBytes);
        var first = new Segment(firstBytes);
        var last = first.Append(bytes.AsMemory(bytes.Length - 1), runningIndex: bytes.Length - 1);
        var result = new ReadOnlySequence<byte>(first, 0, last, 1);
        Assert.Equal(bytes.Length, result.Length);
        return result;
    }

    private static ReadOnlySequence<byte> WithEmptySegments(byte[] bytes, int emptySegments)
    {
        var first = new Segment(ReadOnlyMemory<byte>.Empty);
        var last = first;
        for (var index = 1; index < emptySegments; index++) last = last.Append(ReadOnlyMemory<byte>.Empty);
        last = last.Append(bytes);
        return new(first, 0, last, bytes.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> nextMemory, long? runningIndex = null)
        {
            var next = new Segment(nextMemory) { RunningIndex = runningIndex ?? RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
