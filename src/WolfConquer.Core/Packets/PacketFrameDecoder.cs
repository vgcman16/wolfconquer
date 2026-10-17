using System.Buffers;
using System.IO;

namespace WolfConquer.Core.Packets;

public static class PacketFrameDecoder
{
    public static bool TryRead(
        ReadOnlySequence<byte> buffer,
        out PacketFrame frame,
        out SequencePosition consumed,
        out SequencePosition examined,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        if (maxFrameSize < PacketHeader.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "Maximum frame size cannot be smaller than the packet header.");
        }

        if (buffer.Length < PacketHeader.Size)
        {
            frame = default;
            consumed = buffer.Start;
            examined = buffer.End;
            return false;
        }

        Span<byte> headerBytes = stackalloc byte[PacketHeader.Size];
        if (buffer.FirstSpan.Length >= PacketHeader.Size)
        {
            buffer.FirstSpan[..PacketHeader.Size].CopyTo(headerBytes);
        }
        else
        {
            buffer.Slice(0, PacketHeader.Size).CopyTo(headerBytes);
        }

        if (!PacketHeader.TryRead(headerBytes, out var header))
        {
            frame = default;
            consumed = buffer.Start;
            examined = buffer.End;
            return false;
        }

        if (header.Length < PacketHeader.Size)
        {
            throw new InvalidDataException($"Packet frame length {header.Length} is smaller than the header size.");
        }

        if (header.Length > maxFrameSize)
        {
            throw new InvalidDataException($"Packet frame length {header.Length} exceeds the configured maximum size {maxFrameSize}.");
        }

        if (buffer.Length < header.Length)
        {
            frame = default;
            consumed = buffer.Start;
            examined = buffer.End;
            return false;
        }

        var frameBytes = buffer.Slice(0, header.Length);
        consumed = buffer.GetPosition(header.Length);
        examined = consumed;
        frame = new PacketFrame(header, frameBytes, frameBytes.Slice(PacketHeader.Size));
        return true;
    }
}
