using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyPoisonedStatusFlag = 1;
    private const string RuntimeLegacyPoisonLevelAttribute = "PoisonLevel";
    private const string RuntimeLegacyPoisonedExpiresAtUtcAttribute = "LegacyPoisonedExpiresAtUtc";
    private const string RuntimeLegacyPoisonExpiresAtUtcAttribute = "LegacyPoisonExpiresAtUtc";
    private const string RuntimeLegacyToxicPoisonExpiresAtUtcAttribute = "LegacyToxicPoisonExpiresAtUtc";
    private const string RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute = "LegacyToxicFogPoisonExpiresAtUtc";

    private static readonly string[] RuntimeLegacyPoisonedExpiryAttributes =
    [
        RuntimeLegacyPoisonedExpiresAtUtcAttribute,
        RuntimeLegacyPoisonExpiresAtUtcAttribute,
        RuntimeLegacyToxicPoisonExpiresAtUtcAttribute,
        RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeLegacyPoisonedAttributesToRemove =
    [
        RuntimeLegacyPoisonLevelAttribute,
        RuntimeLegacyPoisonedExpiresAtUtcAttribute,
        RuntimeLegacyPoisonExpiresAtUtcAttribute,
        RuntimeLegacyToxicPoisonExpiresAtUtcAttribute,
        RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute,
    ];

    private async ValueTask SendDueLegacyPoisonedExpirationsAsync(
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
                !TryExpireLegacyPoisoned(
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
                    $"WorldServer expired legacy Poisoned flags playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyPoisoned(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        foreach (var expiresAtAttribute in RuntimeLegacyPoisonedExpiryAttributes)
        {
            if (TryExpireLegacyTimedStatusFlag(
                    character,
                    now,
                    LegacyPoisonedStatusFlag,
                    expiresAtAttribute,
                    out updatedCharacter,
                    out removedVisibleFlag,
                    RuntimeLegacyPoisonedAttributesToRemove))
            {
                return true;
            }
        }

        updatedCharacter = character;
        removedVisibleFlag = false;
        return false;
    }
}
