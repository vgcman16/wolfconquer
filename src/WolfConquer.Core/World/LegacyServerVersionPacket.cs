using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyServerVersionPacket
{
    private const int Reserved0Offset = 0;
    private const int Reserved1Offset = Reserved0Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyServerVersion;
    public const int PayloadLength = Reserved1Offset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(PacketFrame frame, out LegacyServerVersionPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(Reserved0Offset) != 0 ||
            reader.ReadUInt32(Reserved1Offset) != 0)
        {
            return false;
        }

        packet = new LegacyServerVersionPacket();
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(Reserved0Offset, 0);
        writer.WriteUInt32(Reserved1Offset, 0);
        return writer.ToArray();
    }
}
