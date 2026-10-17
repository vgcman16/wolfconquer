using System.Buffers.Binary;

namespace WolfConquer.Core.Packets;

public readonly record struct PacketHeader(ushort Length, ushort Opcode)
{
    public const int Size = 4;
    public const int DefaultMaxFrameSize = 6844;

    public static bool TryRead(ReadOnlySpan<byte> buffer, out PacketHeader header)
    {
        if (buffer.Length < Size)
        {
            header = default;
            return false;
        }

        header = new PacketHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..]));
        return true;
    }

    public void WriteTo(Span<byte> buffer)
    {
        if (buffer.Length < Size)
        {
            throw new ArgumentException("Packet header buffer is too small.", nameof(buffer));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(buffer, Length);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[2..], Opcode);
    }
}
