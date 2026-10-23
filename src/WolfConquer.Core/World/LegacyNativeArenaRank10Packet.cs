using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaRank10Packet
{
    // Whole native MsgBuilder767..794. Duplicate LastSeasonRank is literal.
    public static LegacyNativePacket ArenaRank10Create(this LegacyNativePacket stream, LegacyNativeArenaRuntime.ArenaIDs Typ)
    {
        stream.InitWriter();
        stream.Write((uint)Typ);
        stream.Write(1);
        return stream;
    }

    public static LegacyNativePacket AddItemArenaRank10(this LegacyNativePacket stream, LegacyNativeArenaUser user)
    {
        stream.Write(user.UID);
        stream.Write(user.Name, 32);
        stream.Write(user.Mesh);
        stream.Write((uint)user.Level);
        stream.Write(user.Class);
        stream.Write(user.LastSeasonRank);
        stream.Write(user.LastSeasonRank);
        stream.Write(user.LastSeasonArenaPoints);
        stream.Write(user.LastSeasonWin);
        stream.Write(user.LastSeasonLose);
        return stream;
    }

    public static LegacyNativePacket ArenaRank10Finalize(this LegacyNativePacket stream)
    {
        stream.Finalize(2293);
        return stream;
    }
}
