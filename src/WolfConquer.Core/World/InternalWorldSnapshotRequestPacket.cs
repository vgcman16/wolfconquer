using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record InternalWorldSnapshotRequestPacket(long AccountId)
{
    public const int PayloadLength = sizeof(long);

    public static bool TryParse(PacketFrame frame, out InternalWorldSnapshotRequestPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != WorldPacketOpcodes.InternalSnapshotRequest ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        if (!PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new InternalWorldSnapshotRequestPacket(reader.ReadInt64(0));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.InternalSnapshotRequest, PayloadLength);
        writer.WriteInt64(0, AccountId);
        return writer.ToArray();
    }
}
