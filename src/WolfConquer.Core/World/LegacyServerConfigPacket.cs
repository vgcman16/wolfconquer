using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyServerConfigPacket
{
    private const int Reserved0Offset = 0;
    private const int Reserved1Offset = Reserved0Offset + sizeof(uint);
    private const int Value15Offset = Reserved1Offset + sizeof(uint);
    private const int Value9999Offset = Value15Offset + sizeof(uint);
    private const int Value3Offset = Value9999Offset + sizeof(uint);
    private const int FlagOffset = Value3Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyServerConfig;
    public const int PayloadLength = FlagOffset + sizeof(byte);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(PacketFrame frame, out LegacyServerConfigPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(Reserved0Offset) != 0 ||
            reader.ReadUInt32(Reserved1Offset) != 0 ||
            reader.ReadUInt32(Value15Offset) != 15 ||
            reader.ReadUInt32(Value9999Offset) != 9999 ||
            reader.ReadUInt32(Value3Offset) != 3 ||
            reader.Payload[FlagOffset] != 1)
        {
            return false;
        }

        packet = new LegacyServerConfigPacket();
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(Reserved0Offset, 0);
        writer.WriteUInt32(Reserved1Offset, 0);
        writer.WriteUInt32(Value15Offset, 15);
        writer.WriteUInt32(Value9999Offset, 9999);
        writer.WriteUInt32(Value3Offset, 3);
        writer.Payload[FlagOffset] = 1;
        return writer.ToArray();
    }
}
