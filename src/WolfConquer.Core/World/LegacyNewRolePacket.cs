using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyNewRolePacket(string Name, ushort Body, ushort ClassSelection)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNewRole;
    public const int NameOffset = 36;
    public const int NameLength = 32;
    public const int BodyOffset = NameOffset + NameLength + 64;
    public const int ClassSelectionOffset = BodyOffset + sizeof(ushort);
    public const int MinimumPayloadLength = ClassSelectionOffset + sizeof(ushort);

    public static bool TryParse(PacketFrame frame, out LegacyNewRolePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < MinimumPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyNewRolePacket(
            reader.ReadCString(NameOffset, NameLength, NameEncoding),
            reader.ReadUInt16(BodyOffset),
            reader.ReadUInt16(ClassSelectionOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, MinimumPayloadLength);
        var nameBytes = NameEncoding.GetBytes(Name);
        nameBytes.AsSpan(0, Math.Min(nameBytes.Length, NameLength - 1))
            .CopyTo(writer.Payload.Slice(NameOffset, NameLength));
        writer.WriteUInt16(BodyOffset, Body);
        writer.WriteUInt16(ClassSelectionOffset, ClassSelection);
        return writer.ToArray();
    }
}
