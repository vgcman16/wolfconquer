namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private void RemoveExpiredLegacyQualifierMatches(DateTimeOffset nowUtc)
    {
        _arenaStates.RemoveExpiredEndedMatches(nowUtc);
        _teamArenaStates.RemoveExpiredEndedMatches(nowUtc);
        _elitePkStates.RemoveExpiredEndedMatches(nowUtc);
        _teamPkStates.RemoveExpiredEndedMatches(nowUtc);
    }
}
