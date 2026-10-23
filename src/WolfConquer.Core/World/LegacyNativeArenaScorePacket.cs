using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaScorePacket
{
    // Whole native MsgBuilder751..765. Server identity is the real configured owner.
    public static LegacyNativePacket ArenaMatchScoreCreate(this LegacyNativePacket stream,
        uint UidOne, string nameone, uint damageone, uint UidTwo, string nametwo, uint damagetwo)
    {
        stream.InitWriter();
        stream.Write(UidOne);
        stream.Write(LegacyNativeGroupServerList.MyServerInfo.ID);
        stream.Write(nameone, 32);
        stream.Write(damageone);
        stream.Write(UidTwo);
        stream.Write(LegacyNativeGroupServerList.MyServerInfo.ID);
        stream.Write(nametwo, 32);
        stream.Write(damagetwo);
        stream.Write(uint.MaxValue);
        stream.Finalize(2059);
        return stream;
    }
}
