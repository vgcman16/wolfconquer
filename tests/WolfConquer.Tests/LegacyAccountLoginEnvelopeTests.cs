using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class LegacyAccountLoginEnvelopeTests
{
    public enum PacketKind
    {
        PasswordSeed,
        Authentication,
        Forward,
    }

    public enum EnvelopeDefect
    {
        DefaultDeclaredHeader,
        WrongDeclaredOpcode,
        WrongDeclaredLength,
        WrongEmbeddedOpcode,
        ZeroEmbeddedLength,
        HeaderOnlyEmbeddedLength,
        ShortEmbeddedLength,
        LongEmbeddedLength,
        MaximumEmbeddedLength,
        MissingPhysicalFrame,
        IncompletePhysicalHeader,
        TruncatedPhysicalFrame,
        TrailingPhysicalByte,
        JoinedPhysicalFrames,
        MissingPayload,
        TruncatedPayload,
        TrailingPayloadByte,
        DifferentPayloadFirstByte,
        DifferentPayloadLastByte,
        SegmentedWrongEmbeddedOpcode,
        SegmentedTruncatedFrame,
        SegmentedTrailingFrame,
        SegmentedDifferentPayload,
        SegmentedPhysicalShortMetadata,
        SegmentedPhysicalLongMetadata,
        SegmentedPayloadShortMetadata,
        SegmentedPayloadLongMetadata,
    }

    public static IEnumerable<object[]> MalformedEnvelopes()
    {
        foreach (var kind in Enum.GetValues<PacketKind>())
        {
            foreach (var defect in Enum.GetValues<EnvelopeDefect>())
            {
                yield return [kind, defect];
            }
        }
    }

    [Theory]
    [MemberData(nameof(MalformedEnvelopes))]
    public void MalformedEnvelopeRejectsWithoutThrowingOrPublishingOutput(PacketKind kind, EnvelopeDefect defect)
    {
        AssertRejected(kind, CreateMalformedEnvelope(kind, defect));
    }

    [Theory]
    [InlineData(PacketKind.PasswordSeed)]
    [InlineData(PacketKind.Authentication)]
    [InlineData(PacketKind.Forward)]
    public void DefaultFrameRejectsWithoutThrowingOrPublishingOutput(PacketKind kind)
    {
        AssertRejected(kind, default);
    }

    [Theory]
    [InlineData(PacketKind.PasswordSeed)]
    [InlineData(PacketKind.Authentication)]
    [InlineData(PacketKind.Forward)]
    public void EveryCoherentPhysicalSplitPreservesPacket(PacketKind kind)
    {
        var bytes = CreateValidBytes(kind);
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        for (var split = 1; split < bytes.Length; split++)
        {
            var physical = Split(bytes, split);
            AssertAccepted(kind, new PacketFrame(header, physical, physical.Slice(PacketHeader.Size)));
        }
    }

    [Theory]
    [InlineData(PacketKind.PasswordSeed)]
    [InlineData(PacketKind.Authentication)]
    [InlineData(PacketKind.Forward)]
    public void CoherentIndependentPayloadCopiesAreAcceptedAtEverySplit(PacketKind kind)
    {
        var bytes = CreateValidBytes(kind);
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        var payload = bytes[PacketHeader.Size..];
        for (var split = 1; split < payload.Length; split++)
        {
            AssertAccepted(kind, new PacketFrame(header, new ReadOnlySequence<byte>(bytes), Split(payload, split)));
        }
    }

    [Theory]
    [InlineData(PacketKind.PasswordSeed)]
    [InlineData(PacketKind.Authentication)]
    [InlineData(PacketKind.Forward)]
    public void CompleteCoherentPacketWithIndependentContiguousPayloadIsAccepted(PacketKind kind)
    {
        var bytes = CreateValidBytes(kind);
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        AssertAccepted(kind, new PacketFrame(
            header,
            new ReadOnlySequence<byte>(bytes),
            new ReadOnlySequence<byte>(bytes[PacketHeader.Size..])));
    }

    private static PacketFrame CreateMalformedEnvelope(PacketKind kind, EnvelopeDefect defect)
    {
        var bytes = CreateValidBytes(kind);
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        var payloadBytes = bytes[PacketHeader.Size..];
        var physical = new ReadOnlySequence<byte>(bytes);
        var payload = new ReadOnlySequence<byte>(payloadBytes);
        switch (defect)
        {
            case EnvelopeDefect.DefaultDeclaredHeader:
                header = default;
                break;
            case EnvelopeDefect.WrongDeclaredOpcode:
                header = header with { Opcode = ushort.MaxValue };
                break;
            case EnvelopeDefect.WrongDeclaredLength:
                header = header with { Length = (ushort)(bytes.Length - 1) };
                break;
            case EnvelopeDefect.WrongEmbeddedOpcode:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), ushort.MaxValue);
                break;
            case EnvelopeDefect.ZeroEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0);
                break;
            case EnvelopeDefect.HeaderOnlyEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, PacketHeader.Size);
                break;
            case EnvelopeDefect.ShortEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(bytes.Length - 1));
                break;
            case EnvelopeDefect.LongEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(bytes.Length + 1));
                break;
            case EnvelopeDefect.MaximumEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, ushort.MaxValue);
                break;
            case EnvelopeDefect.MissingPhysicalFrame:
                physical = default;
                break;
            case EnvelopeDefect.IncompletePhysicalHeader:
                physical = new ReadOnlySequence<byte>(bytes[..3]);
                break;
            case EnvelopeDefect.TruncatedPhysicalFrame:
                physical = new ReadOnlySequence<byte>(bytes[..^1]);
                break;
            case EnvelopeDefect.TrailingPhysicalByte:
                physical = new ReadOnlySequence<byte>(AppendByte(bytes));
                break;
            case EnvelopeDefect.JoinedPhysicalFrames:
                physical = new ReadOnlySequence<byte>(bytes.Concat(bytes).ToArray());
                break;
            case EnvelopeDefect.MissingPayload:
                payload = default;
                break;
            case EnvelopeDefect.TruncatedPayload:
                payload = new ReadOnlySequence<byte>(payloadBytes[..^1]);
                break;
            case EnvelopeDefect.TrailingPayloadByte:
                payload = new ReadOnlySequence<byte>(AppendByte(payloadBytes));
                break;
            case EnvelopeDefect.DifferentPayloadFirstByte:
                payloadBytes[0] ^= 0xff;
                break;
            case EnvelopeDefect.DifferentPayloadLastByte:
                payloadBytes[^1] ^= 0xff;
                break;
            case EnvelopeDefect.SegmentedWrongEmbeddedOpcode:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), ushort.MaxValue);
                physical = Split(bytes, 3);
                break;
            case EnvelopeDefect.SegmentedTruncatedFrame:
                physical = Split(bytes[..^1], 2);
                break;
            case EnvelopeDefect.SegmentedTrailingFrame:
                physical = Split(AppendByte(bytes), 3);
                break;
            case EnvelopeDefect.SegmentedDifferentPayload:
                payloadBytes[^1] ^= 0xff;
                payload = Split(payloadBytes, 1);
                break;
            case EnvelopeDefect.SegmentedPhysicalShortMetadata:
                physical = WithIncoherentRunningIndex(bytes, actualLengthDelta: -1);
                break;
            case EnvelopeDefect.SegmentedPhysicalLongMetadata:
                physical = WithIncoherentRunningIndex(bytes, actualLengthDelta: 1);
                break;
            case EnvelopeDefect.SegmentedPayloadShortMetadata:
                payload = WithIncoherentRunningIndex(payloadBytes, actualLengthDelta: -1);
                break;
            case EnvelopeDefect.SegmentedPayloadLongMetadata:
                payload = WithIncoherentRunningIndex(payloadBytes, actualLengthDelta: 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(defect));
        }

        return new PacketFrame(header, physical, payload);
    }

    private static byte[] CreateValidBytes(PacketKind kind)
    {
        return kind switch
        {
            PacketKind.PasswordSeed => new LegacyAccountPasswordSeedPacket(0x12345678).ToFrameBytes(),
            PacketKind.Authentication => new LegacyAccountAuthenticationPacket("d\u00e9mon2", "tommysport13", "W\u00f6lf").ToFrameBytes(),
            PacketKind.Forward => new LegacyAccountForwardPacket(0x0013BEED, LegacyAccountForwardType.Ready, 5818, "127.0.0.1").ToFrameBytes(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static void AssertRejected(PacketKind kind, PacketFrame frame)
    {
        switch (kind)
        {
            case PacketKind.PasswordSeed:
                Assert.False(LegacyAccountPasswordSeedPacket.TryParse(frame, out var seed));
                Assert.Equal(default, seed);
                break;
            case PacketKind.Authentication:
                Assert.False(LegacyAccountAuthenticationPacket.TryParse(frame, out var authentication));
                Assert.Null(authentication);
                break;
            case PacketKind.Forward:
                Assert.False(LegacyAccountForwardPacket.TryParse(frame, out var forward));
                Assert.Equal(default, forward);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void AssertAccepted(PacketKind kind, PacketFrame frame)
    {
        switch (kind)
        {
            case PacketKind.PasswordSeed:
                Assert.True(LegacyAccountPasswordSeedPacket.TryParse(frame, out var seed));
                Assert.Equal(new LegacyAccountPasswordSeedPacket(0x12345678), seed);
                break;
            case PacketKind.Authentication:
                Assert.True(LegacyAccountAuthenticationPacket.TryParse(frame, out var authentication));
                Assert.Equal("d\u00e9mon2", authentication.Username);
                Assert.Equal("W\u00f6lf", authentication.Server);
                Assert.Contains("tommysport13", authentication.PasswordCandidates);
                break;
            case PacketKind.Forward:
                Assert.True(LegacyAccountForwardPacket.TryParse(frame, out var forward));
                Assert.Equal(new LegacyAccountForwardPacket(0x0013BEED, LegacyAccountForwardType.Ready, 5818, "127.0.0.1"), forward);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static byte[] AppendByte(byte[] bytes)
    {
        var result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        result[^1] = 0xa5;
        return result;
    }

    private static ReadOnlySequence<byte> Split(byte[] bytes, int split)
    {
        var first = new BufferSegment(bytes.AsMemory(0, split));
        var last = first.Append(bytes.AsMemory(split), split);
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private static ReadOnlySequence<byte> WithIncoherentRunningIndex(byte[] bytes, int actualLengthDelta)
    {
        var firstLength = bytes.Length - 1 + actualLengthDelta;
        var firstBytes = new byte[firstLength];
        bytes.AsSpan(0, Math.Min(firstLength, bytes.Length)).CopyTo(firstBytes);
        var first = new BufferSegment(firstBytes);
        var last = first.Append(bytes.AsMemory(bytes.Length - 1), bytes.Length - 1);
        var result = new ReadOnlySequence<byte>(first, 0, last, 1);
        Assert.Equal(bytes.Length, result.Length);
        return result;
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(Memory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(Memory<byte> memory, long runningIndex)
        {
            var next = new BufferSegment(memory) { RunningIndex = runningIndex };
            Next = next;
            return next;
        }
    }
}
