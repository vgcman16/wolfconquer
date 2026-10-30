using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaScorePacket
{
    // Whole native MsgBuilder751..765. Server identity is the real configured owner.
    public static LegacyNativePacket ArenaMatchScoreCreate(this LegacyNativePacket stream,
        uint UidOne, string nameone, uint damageone, uint UidTwo, string nametwo, uint damagetwo)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(nameone);
        ArgumentNullException.ThrowIfNull(nametwo);
        var server = LegacyNativeGroupServerList.MyServerInfo
            ?? throw new InvalidOperationException("Arena score requires the configured server owner.");
        uint serverId = server.ID;
        stream.PrepareFrame(2059, prepared =>
        {
            prepared.Write(UidOne);
            prepared.Write(serverId);
            prepared.Write(nameone, 32);
            prepared.Write(damageone);
            prepared.Write(UidTwo);
            prepared.Write(serverId);
            prepared.Write(nametwo, 32);
            prepared.Write(damagetwo);
            prepared.Write(uint.MaxValue);
        });
        return stream;
    }
}
