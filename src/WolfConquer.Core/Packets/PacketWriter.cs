using System.Buffers.Binary;

namespace WolfConquer.Core.Packets;

public sealed class PacketWriter
{
    private readonly byte[] _buffer;

    public PacketWriter(ushort opcode, int payloadLength, int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        if (payloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength), "Payload length cannot be negative.");
        }

        var frameLength = checked(PacketHeader.Size + payloadLength);
        if (frameLength > ushort.MaxValue || frameLength > maxFrameSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength), "Packet frame exceeds the configured maximum size.");
        }

        _buffer = new byte[frameLength];
        Header = new PacketHeader((ushort)frameLength, opcode);
        Header.WriteTo(_buffer);
    }

    public PacketHeader Header { get; }

    public Span<byte> Payload => _buffer.AsSpan(PacketHeader.Size);

    public byte[] ToArray()
    {
        return (byte[])_buffer.Clone();
    }

    public void WriteUInt16(int payloadOffset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(GetPayloadSlice(payloadOffset, sizeof(ushort)), value);
    }

    public void WriteUInt32(int payloadOffset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(GetPayloadSlice(payloadOffset, sizeof(uint)), value);
    }

    public void WriteInt32(int payloadOffset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(GetPayloadSlice(payloadOffset, sizeof(int)), value);
    }

    public void WriteUInt64(int payloadOffset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(GetPayloadSlice(payloadOffset, sizeof(ulong)), value);
    }

    public void WriteInt64(int payloadOffset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(GetPayloadSlice(payloadOffset, sizeof(long)), value);
    }

    private Span<byte> GetPayloadSlice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > Payload.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Packet payload write is out of range.");
        }

        return Payload.Slice(offset, length);
    }
}
