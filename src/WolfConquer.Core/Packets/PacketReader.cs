using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.Packets;

public readonly ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _frame;

    private PacketReader(ReadOnlySpan<byte> frame, PacketHeader header)
    {
        _frame = frame;
        Header = header;
        Payload = frame[PacketHeader.Size..header.Length];
    }

    public PacketHeader Header { get; }

    public ReadOnlySpan<byte> Payload { get; }

    public static bool TryRead(ReadOnlySpan<byte> buffer, out PacketReader reader, int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        if (!PacketHeader.TryRead(buffer, out var header) ||
            header.Length < PacketHeader.Size ||
            header.Length > maxFrameSize ||
            buffer.Length < header.Length)
        {
            reader = default;
            return false;
        }

        reader = new PacketReader(buffer[..header.Length], header);
        return true;
    }

    public ushort ReadUInt16(int payloadOffset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(GetPayloadSlice(payloadOffset, sizeof(ushort)));
    }

    public uint ReadUInt32(int payloadOffset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(GetPayloadSlice(payloadOffset, sizeof(uint)));
    }

    public int ReadInt32(int payloadOffset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(GetPayloadSlice(payloadOffset, sizeof(int)));
    }

    public ulong ReadUInt64(int payloadOffset)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(GetPayloadSlice(payloadOffset, sizeof(ulong)));
    }

    public long ReadInt64(int payloadOffset)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(GetPayloadSlice(payloadOffset, sizeof(long)));
    }

    public string ReadCString(int payloadOffset, int maxLength, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Maximum string length cannot be negative.");
        }

        var bytes = GetPayloadSlice(payloadOffset, maxLength);
        var length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return encoding.GetString(bytes[..length]);
    }

    private ReadOnlySpan<byte> GetPayloadSlice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > Payload.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Packet payload read is out of range.");
        }

        return Payload.Slice(offset, length);
    }
}
