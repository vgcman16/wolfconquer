using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyCleanSweepStatusFlag = 0xFE;
    private const string RuntimeCleanSweepPowerAttribute = "CleanSweepPower";
    private const string RuntimeCleanSweepExpiresAtUtcAttribute = "CleanSweepExpiresAtUtc";
    private const string RuntimeLegacyCleanSweepPowerAttribute = "LegacyCleanSweepPower";
    private const string RuntimeLegacyCleanSweepExpiresAtUtcAttribute = "LegacyCleanSweepExpiresAtUtc";

    private static readonly string[] RuntimeCleanSweepExpiryAttributes =
    [
        RuntimeLegacyCleanSweepExpiresAtUtcAttribute,
        RuntimeCleanSweepExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeCleanSweepAttributesToRemove =
    [
        RuntimeCleanSweepPowerAttribute,
        RuntimeCleanSweepExpiresAtUtcAttribute,
        RuntimeLegacyCleanSweepPowerAttribute,
        RuntimeLegacyCleanSweepExpiresAtUtcAttribute,
    ];

    private async ValueTask SendDueLegacyCleanSweepExpirationsAsync(
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
                !TryExpireLegacyCleanSweep(
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
                    "WorldServer expired legacy CleanSweep flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyCleanSweep(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        foreach (var expiresAtAttribute in RuntimeCleanSweepExpiryAttributes)
        {
            if (TryExpireLegacyTimedStatusFlag(
                    character,
                    now,
                    LegacyCleanSweepStatusFlag,
                    expiresAtAttribute,
                    out updatedCharacter,
                    out removedVisibleFlag,
                    RuntimeCleanSweepAttributesToRemove))
            {
                return true;
            }
        }

        updatedCharacter = character;
        removedVisibleFlag = false;
        return false;
    }
}
