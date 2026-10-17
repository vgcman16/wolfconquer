using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private ValueTask ProcessDueLegacyVoteClaimRuntimeTickAsync(
        ConquerTcpConnection connection,
        WorldSession? currentSession,
        DateTimeOffset nowUtc,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var context = new WorldPacketHandlerContext(
            connection,
            _log,
            Session: currentSession,
            Sessions: _sessions,
            VoteStates: _voteStates,
            TimeProvider: timeProvider);
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeProvider.LocalTimeZone).DateTime;
        return WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            nowUtc,
            localNow,
            cancellationToken);
    }
}
