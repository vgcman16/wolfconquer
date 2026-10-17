using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTitleAction : byte
{
    Enqueue = 1,
    Dequeue = 2,
    Change = 3,
    List = 4,
}

public readonly record struct LegacyTitlePacket(
    uint Uid,
    byte Title,
    LegacyTitleAction Action)
{
    private const int UidOffset = 0;
    private const int TitleOffset = UidOffset + sizeof(uint);
    private const int ActionOffset = TitleOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTitle;
    public const int PayloadLength = ActionOffset + sizeof(byte);

    public static bool TryParse(PacketFrame frame, out LegacyTitlePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyTitlePacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            payload[TitleOffset],
            (LegacyTitleAction)payload[ActionOffset]);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.Payload[TitleOffset] = Title;
        writer.Payload[ActionOffset] = (byte)Action;
        return writer.ToArray();
    }
}
