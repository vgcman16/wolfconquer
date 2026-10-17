using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyKillingFlashXpListStatusFlag = 0x04;
    private const string RuntimeLegacyKillingFlashXpListExpiresAtUtcAttribute =
        "RuntimeLegacyKillingFlashXpListExpiresAtUtc";

    private async ValueTask SendDueLegacyKillingFlashXpListExpirationsAsync(
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
                !TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    LegacyKillingFlashXpListStatusFlag,
                    RuntimeLegacyKillingFlashXpListExpiresAtUtcAttribute,
                    out var expiredCharacter,
                    out var removedVisibleFlag))
            {
                continue;
            }

            var updatedSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, updatedSession);

            var uid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            await active.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!removedVisibleFlag)
            {
                playerCount++;
                continue;
            }

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
                    "WorldServer expired legacy KillingFlash XPList flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }
}
