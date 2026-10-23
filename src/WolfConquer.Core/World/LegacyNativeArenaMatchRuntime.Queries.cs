namespace WolfConquer.Core.World;

internal partial class LegacyNativeArenaMatchRuntime
{
    // Whole native Match287..305. SingleOrDefault and Players[0] fallbacks are literal.
    public LegacyNativeGameClientRuntime Winner()
    {
        LegacyNativeGameClientRuntime? gameClient = Players.Where((LegacyNativeGameClientRuntime p) =>
            p.ArenaStatistic!.QualifierStatus != LegacyNativeArenaUser.MatchStatus.Loser &&
            p.ArenaStatistic!.QualifierStatus != LegacyNativeArenaUser.MatchStatus.None).SingleOrDefault();
        if (gameClient == null)
        {
            return Players[0];
        }
        return gameClient;
    }

    public LegacyNativeGameClientRuntime Loser()
    {
        LegacyNativeGameClientRuntime? gameClient = Players.Where((LegacyNativeGameClientRuntime p) =>
            p.ArenaStatistic!.QualifierStatus == LegacyNativeArenaUser.MatchStatus.Loser).SingleOrDefault();
        if (gameClient == null)
        {
            return Players[0];
        }
        return gameClient;
    }

    // Whole native Match320..333. Does not first verify that MyUID belongs here.
    public bool TryGetOpponent(uint MyUID, out LegacyNativeGameClientRuntime? client)
    {
        LegacyNativeGameClientRuntime[] players = Players;
        foreach (LegacyNativeGameClientRuntime gameClient in players)
        {
            if (MyUID != gameClient.Player.UID)
            {
                client = gameClient;
                return true;
            }
        }
        client = null;
        return false;
    }
}
