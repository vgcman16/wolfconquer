using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyAxeShadowStatusFlag = 0xFB;
    private const int LegacyCelestialDanceStatusFlag = 0xFA;
    private const string RuntimeAxeShadowPowerAttribute = "AxeShadowPower";
    private const string RuntimeLegacyAxeShadowPowerAttribute = "LegacyAxeShadowPower";
    private const string RuntimeAxeShadowExpiresAtUtcAttribute = "AxeShadowExpiresAtUtc";
    private const string RuntimeLegacyAxeShadowExpiresAtUtcAttribute = "LegacyAxeShadowExpiresAtUtc";
    private const string RuntimeCelestialDancePowerAttribute = "CelestialDancePower";
    private const string RuntimeLegacyCelestialDancePowerAttribute = "LegacyCelestialDancePower";
    private const string RuntimeCelestialDanceExpiresAtUtcAttribute = "CelestialDanceExpiresAtUtc";
    private const string RuntimeLegacyCelestialDanceExpiresAtUtcAttribute = "LegacyCelestialDanceExpiresAtUtc";

    private static readonly string[] RuntimeAxeShadowExpiryAttributes =
    [
        RuntimeLegacyAxeShadowExpiresAtUtcAttribute,
        RuntimeAxeShadowExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeAxeShadowAttributesToRemove =
    [
        RuntimeAxeShadowPowerAttribute,
        RuntimeLegacyAxeShadowPowerAttribute,
        RuntimeAxeShadowExpiresAtUtcAttribute,
        RuntimeLegacyAxeShadowExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeCelestialDanceExpiryAttributes =
    [
        RuntimeLegacyCelestialDanceExpiresAtUtcAttribute,
        RuntimeCelestialDanceExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeCelestialDanceAttributesToRemove =
    [
        RuntimeCelestialDancePowerAttribute,
        RuntimeLegacyCelestialDancePowerAttribute,
        RuntimeCelestialDanceExpiresAtUtcAttribute,
        RuntimeLegacyCelestialDanceExpiresAtUtcAttribute,
    ];

    private async ValueTask SendDueLegacyAxeShadowCelestialDanceExpirationsAsync(
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
                !TryExpireLegacyAxeShadowCelestialDanceStatusFlags(
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
                    "WorldServer expired legacy AxeShadow/CelestialDance flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyAxeShadowCelestialDanceStatusFlags(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        var changed = false;
        removedVisibleFlag = false;
        updatedCharacter = character;

        foreach (var expiresAtAttribute in RuntimeAxeShadowExpiryAttributes)
        {
            if (TryExpireLegacyTimedStatusFlag(
                    updatedCharacter,
                    now,
                    LegacyAxeShadowStatusFlag,
                    expiresAtAttribute,
                    out updatedCharacter,
                    out var removedAxeShadowFlag,
                    RuntimeAxeShadowAttributesToRemove))
            {
                changed = true;
                removedVisibleFlag |= removedAxeShadowFlag;
                break;
            }
        }

        foreach (var expiresAtAttribute in RuntimeCelestialDanceExpiryAttributes)
        {
            if (TryExpireLegacyTimedStatusFlag(
                    updatedCharacter,
                    now,
                    LegacyCelestialDanceStatusFlag,
                    expiresAtAttribute,
                    out updatedCharacter,
                    out var removedCelestialDanceFlag,
                    RuntimeCelestialDanceAttributesToRemove))
            {
                changed = true;
                removedVisibleFlag |= removedCelestialDanceFlag;
                break;
            }
        }

        return changed;
    }
}
