using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyAutoHuntPacket(ushort Type, ulong Parameter)
{
    private const int TypeOffset = 0;
    private const int ParameterOffset = TypeOffset + sizeof(ushort);
    private const int ReservedOneOffset = ParameterOffset + sizeof(ulong);
    private const int ReservedTwoOffset = ReservedOneOffset + sizeof(int);

    public const ushort Opcode = WorldPacketOpcodes.LegacyAutoHunt;
    public const ushort StartupType = 0;
    public const ulong StartupParameter = 341;
    public const int PayloadLength = ReservedTwoOffset + sizeof(int);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static LegacyAutoHuntPacket Startup => new(StartupType, StartupParameter);

    public static bool TryParse(PacketFrame frame, out LegacyAutoHuntPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadInt32(ReservedOneOffset) != 0 ||
            reader.ReadInt32(ReservedTwoOffset) != 0)
        {
            return false;
        }

        packet = new LegacyAutoHuntPacket(
            reader.ReadUInt16(TypeOffset),
            reader.ReadUInt64(ParameterOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(TypeOffset, Type);
        writer.WriteUInt64(ParameterOffset, Parameter);
        return writer.ToArray();
    }
}
