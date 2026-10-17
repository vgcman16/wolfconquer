using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyMsgLoginGamePacket(uint Key)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgLoginGame;
    public const int PayloadLength = sizeof(uint);
    public const int CapturedClientPayloadLength = 48;
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(PacketFrame frame, out LegacyMsgLoginGamePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            (frame.Payload.Length != PayloadLength && frame.Payload.Length != CapturedClientPayloadLength) ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyMsgLoginGamePacket(reader.ReadUInt32(0));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, Key);
        return writer.ToArray();
    }
}
