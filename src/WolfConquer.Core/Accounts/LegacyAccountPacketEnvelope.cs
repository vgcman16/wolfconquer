using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

internal static class LegacyAccountPacketEnvelope
{
    private const int MaxSegmentVisits = 4096;

    public static bool TryRead(
        PacketFrame frame,
        ushort opcode,
        int frameLength,
        out PacketReader reader)
    {
        reader = default;
        if (frameLength < PacketHeader.Size ||
            frameLength > LegacyAccountAuthenticationPacket.FrameLength ||
            frame.Header.Opcode != opcode ||
            frame.Header.Length != frameLength ||
            frame.Frame.Length != frameLength ||
            frame.Payload.Length != frameLength - PacketHeader.Size)
        {
            return false;
        }

        // Copy only the fixed, bounded physical frame. Never size an allocation
        // from untrusted sequence metadata or a separately declared wire length.
        var bytes = new byte[frameLength];
        if (!TryCopyExact(frame.Frame, bytes) ||
            !PacketHeader.TryRead(bytes, out var embeddedHeader) ||
            embeddedHeader != frame.Header ||
            !HasExactContent(frame.Payload, bytes.AsSpan(PacketHeader.Size)))
        {
            return false;
        }

        return PacketReader.TryRead(bytes, out reader, frameLength);
    }

    private static bool TryCopyExact(ReadOnlySequence<byte> sequence, Span<byte> destination)
    {
        var copied = 0;
        var segments = 0;
        foreach (var memory in sequence)
        {
            if (++segments > MaxSegmentVisits ||
                memory.Length > destination.Length - copied)
            {
                return false;
            }

            memory.Span.CopyTo(destination[copied..]);
            copied += memory.Length;
        }

        return copied == destination.Length;
    }

    private static bool HasExactContent(ReadOnlySequence<byte> sequence, ReadOnlySpan<byte> expected)
    {
        var compared = 0;
        var segments = 0;
        foreach (var memory in sequence)
        {
            if (++segments > MaxSegmentVisits ||
                memory.Length > expected.Length - compared ||
                !memory.Span.SequenceEqual(expected.Slice(compared, memory.Length)))
            {
                return false;
            }

            compared += memory.Length;
        }

        return compared == expected.Length;
    }
}
