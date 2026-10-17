using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static class WorldSnapshotResponsePacket
{
    public const int PayloadLength =
        sizeof(byte) +
        sizeof(long) +
        sizeof(long) +
        sizeof(long) +
        sizeof(long) +
        sizeof(long) +
        sizeof(long) +
        sizeof(long);

    public static byte[] Build(WorldSnapshotWireStatus status, long accountId, CharacterSnapshot? snapshot = null)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.InternalSnapshotResponse, PayloadLength);
        writer.Payload[0] = (byte)status;
        writer.WriteInt64(1, accountId);
        writer.WriteInt64(9, snapshot?.Uid ?? 0);
        writer.WriteInt64(17, snapshot?.MapId ?? 0);
        writer.WriteInt64(25, snapshot?.X ?? 0);
        writer.WriteInt64(33, snapshot?.Y ?? 0);
        writer.WriteInt64(41, snapshot?.Level ?? 0);
        writer.WriteInt64(49, snapshot?.Class ?? 0);
        return writer.ToArray();
    }

    public static WorldSnapshotWireStatus ReadStatus(PacketReader reader)
    {
        return (WorldSnapshotWireStatus)reader.Payload[0];
    }

    public static long ReadAccountId(PacketReader reader)
    {
        return reader.ReadInt64(1);
    }

    public static long ReadUid(PacketReader reader)
    {
        return reader.ReadInt64(9);
    }

    public static long ReadMapId(PacketReader reader)
    {
        return reader.ReadInt64(17);
    }

    public static long ReadX(PacketReader reader)
    {
        return reader.ReadInt64(25);
    }

    public static long ReadY(PacketReader reader)
    {
        return reader.ReadInt64(33);
    }

    public static long ReadLevel(PacketReader reader)
    {
        return reader.ReadInt64(41);
    }

    public static long ReadClass(PacketReader reader)
    {
        return reader.ReadInt64(49);
    }
}
