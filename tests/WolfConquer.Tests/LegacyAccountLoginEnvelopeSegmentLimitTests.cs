using System.Buffers;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using static WolfConquer.Tests.LegacyAccountLoginEnvelopeTests;

namespace WolfConquer.Tests;

public sealed class LegacyAccountLoginEnvelopeSegmentLimitTests
{
    [Theory]
    [InlineData(PacketKind.PasswordSeed, false)]
    [InlineData(PacketKind.Authentication, false)]
    [InlineData(PacketKind.Forward, false)]
    [InlineData(PacketKind.PasswordSeed, true)]
    [InlineData(PacketKind.Authentication, true)]
    [InlineData(PacketKind.Forward, true)]
    public void CoherentEmptySegmentsAtTheVisitLimitRemainAccepted(PacketKind kind, bool segmentedPayload)
    {
        var frame = CreateFrame(kind, segmentedPayload, emptySegments: 4095);
        Assert.True(Parse(kind, frame));
    }

    [Theory]
    [InlineData(PacketKind.PasswordSeed, false)]
    [InlineData(PacketKind.Authentication, false)]
    [InlineData(PacketKind.Forward, false)]
    [InlineData(PacketKind.PasswordSeed, true)]
    [InlineData(PacketKind.Authentication, true)]
    [InlineData(PacketKind.Forward, true)]
    public void TooManyEmptySegmentsRejectWithoutThrowingOrPublishingOutput(PacketKind kind, bool segmentedPayload)
    {
        var frame = CreateFrame(kind, segmentedPayload, emptySegments: 4096);
        Assert.False(Parse(kind, frame));
    }

    private static bool Parse(PacketKind kind, PacketFrame frame)
    {
        switch (kind)
        {
            case PacketKind.PasswordSeed:
                var seedAccepted = LegacyAccountPasswordSeedPacket.TryParse(frame, out var seed);
                if (!seedAccepted) { Assert.Equal(default, seed); }
                return seedAccepted;
            case PacketKind.Authentication:
                var authenticationAccepted = LegacyAccountAuthenticationPacket.TryParse(frame, out var authentication);
                if (!authenticationAccepted) { Assert.Null(authentication); }
                return authenticationAccepted;
            case PacketKind.Forward:
                var forwardAccepted = LegacyAccountForwardPacket.TryParse(frame, out var forward);
                if (!forwardAccepted) { Assert.Equal(default, forward); }
                return forwardAccepted;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static PacketFrame CreateFrame(PacketKind kind, bool segmentedPayload, int emptySegments)
    {
        var bytes = kind switch
        {
            PacketKind.PasswordSeed => new LegacyAccountPasswordSeedPacket(0x12345678).ToFrameBytes(),
            PacketKind.Authentication => new LegacyAccountAuthenticationPacket("2", "2", "Wolf").ToFrameBytes(),
            PacketKind.Forward => new LegacyAccountForwardPacket(1293600, LegacyAccountForwardType.Ready, 5818, "127.0.0.1").ToFrameBytes(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        var payload = bytes[PacketHeader.Size..];
        return segmentedPayload
            ? new PacketFrame(header, new ReadOnlySequence<byte>(bytes), WithEmptySegments(payload, emptySegments))
            : new PacketFrame(header, WithEmptySegments(bytes, emptySegments), new ReadOnlySequence<byte>(payload));
    }

    private static ReadOnlySequence<byte> WithEmptySegments(byte[] bytes, int emptySegments)
    {
        var first = new BufferSegment(ReadOnlyMemory<byte>.Empty);
        var last = first;
        for (var index = 1; index < emptySegments; index++)
        {
            last = last.Append(ReadOnlyMemory<byte>.Empty);
        }

        last = last.Append(bytes);
        return new ReadOnlySequence<byte>(first, 0, last, bytes.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new BufferSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
