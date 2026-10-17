using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyBlackspotPacket(uint Uid, bool Active)
{
    private const int DwParamOffset = 0;
    private const int UidOffset = DwParamOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyBlackspot;
    public const int PayloadLength = UidOffset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public uint DwParam => Active ? 0u : 1u;

    public static LegacyBlackspotPacket CreateActive(uint uid)
    {
        return new LegacyBlackspotPacket(uid, Active: true);
    }

    public static LegacyBlackspotPacket CreateInactive(uint uid)
    {
        return new LegacyBlackspotPacket(uid, Active: false);
    }

    public static bool TryParse(PacketFrame frame, out LegacyBlackspotPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var bytes = new byte[frame.Frame.Length];
        frame.Frame.CopyTo(bytes);
        if (!PacketReader.TryRead(bytes, out var reader))
        {
            return false;
        }

        var dwParam = reader.ReadUInt32(DwParamOffset);
        if (dwParam > 1)
        {
            return false;
        }

        packet = new LegacyBlackspotPacket(
            reader.ReadUInt32(UidOffset),
            Active: dwParam == 0);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(DwParamOffset, DwParam);
        writer.WriteUInt32(UidOffset, Uid);
        return writer.ToArray();
    }
}
