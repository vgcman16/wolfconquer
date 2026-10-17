using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyRideStatusFlag = 0x32;
    private const string RuntimeLegacyRideExpiresAtUtcAttribute = "LegacyRideExpiresAtUtc";
    private const string RuntimeLegacyRideRemoveOnDeadAttribute = "LegacyRideRemoveOnDead";
    private const string RuntimeLegacyRideStampSecondsAttribute = "LegacyRideStampSeconds";

    private async ValueTask SendDueLegacyRideExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var playerCount = 0;
        var viewerCount = 0;
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyRideStatus(
                    currentSession.Character,
                    now,
                    out var expiredCharacter,
                    out var removedVisibleFlag))
            {
                continue;
            }

            var updatedSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, updatedSession);
            if (!removedVisibleFlag)
            {
                continue;
            }

            var uid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            var statusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        expiredCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();

            await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
            viewerCount++;

            foreach (var viewer in activeSessions)
            {
                if (ReferenceEquals(viewer.Connection, active.Connection) ||
                    viewer.Session.Character.Uid == expiredCharacter.Uid ||
                    !CanSeePlayer(viewer.Session.Character, expiredCharacter))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }

            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy Ride flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}; " +
                    "source=ThreadPool.cs flag.Expire and Riding.cs AddFlag(2592000, RemoveOnDead: true, 1).")
                .ConfigureAwait(false);
        }
    }

    internal static bool TryExpireLegacyRideStatus(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        return TryExpireLegacyTimedStatusFlag(
            character,
            now,
            LegacyRideStatusFlag,
            RuntimeLegacyRideExpiresAtUtcAttribute,
            out updatedCharacter,
            out removedVisibleFlag,
            RuntimeLegacyRideRemoveOnDeadAttribute,
            RuntimeLegacyRideStampSecondsAttribute);
    }
}
