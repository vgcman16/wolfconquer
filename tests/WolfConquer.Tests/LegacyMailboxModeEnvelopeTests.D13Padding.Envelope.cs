using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxModeEnvelopeTests
{
    [Fact]
    public void CapturedD13PaddingUsesExactPhysicalAndPayloadSnapshotsAcrossEverySplitAndVisitBudget()
    {
        foreach (var (hex, _) in LegacyMailboxD13CapturedFrames.All)
        {
            var bytes = Convert.FromHexString(hex); var frame = Frame(bytes); var payload = bytes[4..];
            Assert.True(LegacyMailboxModePacket.TryParse(frame, ConquerProtocolProfile.VisibleDx8D13, out var expected));
            void Accepted(PacketFrame segmented)
            {
                Assert.True(LegacyMailboxModePacket.TryParse(segmented, ConquerProtocolProfile.VisibleDx8D13, out var actual));
                Assert.Equal(expected.Type, actual.Type); Assert.Equal(expected.Id, actual.Id); Assert.Equal(expected.Items, actual.Items);
            }
            for (var split = 1; split < bytes.Length; split++) Accepted(frame with { Frame = Split(bytes, split) });
            for (var split = 1; split < payload.Length; split++) Accepted(frame with { Payload = Split(payload, split) });
            Accepted(frame with { Frame = Split(bytes, 3), Payload = Split(payload, 1) });
            Accepted(frame with { Frame = WithEmptySegments(bytes, 4095) });
            Accepted(frame with { Payload = WithEmptySegments(payload, 4095) });
            AssertD13Rejected(frame with { Frame = WithEmptySegments(bytes, 4096) });
            AssertD13Rejected(frame with { Payload = WithEmptySegments(payload, 4096) });
            foreach (var defect in Enum.GetValues<EnvelopeDefect>()) AssertD13Rejected(D13Malformed(bytes, defect));
        }
    }

    [Fact]
    public void PaddedClaimClassificationUsesTheActualSelectedProfileAndDoesNotChangeContentLeasePolicy()
    {
        foreach (var hex in new[] { LegacyMailboxD13CapturedFrames.Page7, LegacyMailboxD13CapturedFrames.Page2 })
        {
            var frame = Frame(Convert.FromHexString(hex));
            Assert.True(WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.VisibleDx8D13));
            Assert.False(WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.RootD11_7899));
        }
        Assert.False(WorldServerFrameHandler.HandlerOwnsOperation(Frame(Convert.FromHexString(LegacyMailboxD13CapturedFrames.Content1)),
            ConquerProtocolProfile.VisibleDx8D13));
    }

    private static PacketFrame D13Malformed(byte[] original, EnvelopeDefect defect)
    {
        var bytes = original.ToArray(); var frame = Frame(bytes); var payload = bytes[4..];
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
            case EnvelopeDefect.DifferentPayload: payload[^1] = 1; return frame with { Payload = new ReadOnlySequence<byte>(payload) };
            case EnvelopeDefect.PhysicalShortMetadata: return frame with { Frame = FalseLength(bytes, -1) };
            case EnvelopeDefect.PhysicalLongMetadata: return frame with { Frame = FalseLength(bytes, 1) };
            case EnvelopeDefect.PayloadShortMetadata: return frame with { Payload = FalseLength(payload, -1) };
            case EnvelopeDefect.PayloadLongMetadata: return frame with { Payload = FalseLength(payload, 1) };
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
    }
}
