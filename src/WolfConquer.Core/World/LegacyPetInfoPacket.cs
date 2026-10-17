using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyPetInfoPacket(
    uint Uid,
    uint PetId,
    uint PetType,
    ulong Mesh,
    ushort Range,
    ushort X,
    ushort Y,
    string Name)
{
    private const int UidOffset = 0;
    private const int PetIdOffset = UidOffset + sizeof(uint);
    private const int PetTypeOffset = PetIdOffset + sizeof(uint);
    private const int MeshOffset = PetTypeOffset + sizeof(uint);
    private const int RangeOffset = MeshOffset + sizeof(ulong);
    private const int XOffset = RangeOffset + sizeof(ushort);
    private const int YOffset = XOffset + sizeof(ushort);
    private const int NameOffset = YOffset + sizeof(ushort);

    public const ushort Opcode = 2192;
    public const int FixedNameLength = 32;
    public const int PayloadLength = NameOffset + FixedNameLength;

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(PetIdOffset, PetId);
        writer.WriteUInt32(PetTypeOffset, PetType);
        writer.WriteUInt64(MeshOffset, Mesh);
        writer.WriteUInt16(RangeOffset, Range);
        writer.WriteUInt16(XOffset, X);
        writer.WriteUInt16(YOffset, Y);
        WriteFixedUtf8(writer.Payload[NameOffset..(NameOffset + FixedNameLength)], Name);
        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyPetInfoPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyPetInfoPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(PetIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(PetTypeOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(MeshOffset, sizeof(ulong))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(RangeOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(XOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(YOffset, sizeof(ushort))),
            ReadFixedUtf8(payload.AsSpan(NameOffset, FixedNameLength)));
        return true;
    }

    private static string ReadFixedUtf8(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        if (length < 0)
        {
            length = source.Length;
        }

        return Encoding.UTF8.GetString(source[..length]);
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}
