using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyControlStatusExpirationMaximumAttempts = 3;

    private async ValueTask SendDueLegacyBossFrightenedExpirationsAsync(
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
            if (!TryCommitDueLegacyControlStatusExpiration(
                    active,
                    now,
                    LegacyBossStatusEffect.FrightenedFlag,
                    LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute,
                    out var expiredCharacter,
                    out var removedVisibleFlag,
                    LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute))
            {
                continue;
            }

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

            await active.Connection.SendAsync(
                    new LegacyGameUpdatePacket(
                            uid,
                            LegacyGameUpdateDataType.Flustered,
                            Active: false,
                            ShownAmount: 0,
                            Time: 0,
                            Amount: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy boss Frightened flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount} " +
                    $"dataType={(uint)LegacyGameUpdateDataType.Flustered}.")
                .ConfigureAwait(false);
        }
    }

    private bool TryCommitDueLegacyControlStatusExpiration(
        WorldActiveSession active,
        DateTimeOffset now,
        int statusFlag,
        string expiresAtAttribute,
        out CharacterSnapshot expiredCharacter,
        out bool removedVisibleFlag,
        string removeOnDeadAttribute)
    {
        expiredCharacter = default!;
        removedVisibleFlag = false;
        for (var attempt = 0; attempt < LegacyControlStatusExpirationMaximumAttempts; attempt++)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    statusFlag,
                    expiresAtAttribute,
                    out var candidateCharacter,
                    out var candidateRemovedVisibleFlag,
                    removeOnDeadAttribute))
            {
                return false;
            }

            var updatedSession = currentSession with { Character = candidateCharacter };
            if (!_sessions.TryCompareAndSet(active.Connection, currentSession, updatedSession))
            {
                continue;
            }

            // A session-changed callback can synchronously publish a re-add before CAS returns.
            if (!_sessions.TryGet(active.Connection, out var publishedSession) ||
                !ReferenceEquals(publishedSession, updatedSession))
            {
                return false;
            }

            expiredCharacter = candidateCharacter;
            removedVisibleFlag = candidateRemovedVisibleFlag;
            return true;
        }

        return false;
    }
}
