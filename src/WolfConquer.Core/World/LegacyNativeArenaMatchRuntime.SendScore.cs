using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeArenaMatchRuntime
{
    // Whole native MsgArena.Match537..551. Reuses real packet recycler and Send.
    public void SendScore()
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        stream = stream.ArenaMatchScoreCreate(Players[0].Player.UID, Players[0].ArenaStatistic!.Name,
            Players[0].ArenaStatistic!.Damage, Players[1].Player.UID, Players[1].ArenaStatistic!.Name,
            Players[1].ArenaStatistic!.Damage);
        LegacyNativeGameClientRuntime[] players = Players;
        foreach (LegacyNativeGameClientRuntime gameClient in players)
        {
            gameClient.Send(stream);
        }
        foreach (LegacyNativeGameClientRuntime value in Watchers.Values)
        {
            value.Send(stream);
        }
    }
}
