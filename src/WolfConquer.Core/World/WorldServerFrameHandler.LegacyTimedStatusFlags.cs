using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyHookMoonStatusFlag = 0x103;
    private const int LegacyHookMoonCasterStatusFlag = 0x104;
    private const string RuntimeLegacyHookMoonExpiresAtUtcAttribute = "HookMoonExpiresAtUtc";
    private const string RuntimeLegacyHookMoonCasterExpiresAtUtcAttribute = "HookMoonCasterExpiresAtUtc";
    private const string RuntimeLegacyHookMoonAttribute = "HookMoon";
    private const string RuntimeLegacyHookMoonActiveAttribute = "HookMoonActive";
    private const string RuntimeLegacyHookMoonAttackedPowerAttribute = "HookMoonAttackedPower";
    private const string RuntimeLegacyHookMoonCasterAttribute = "HookMoonCaster";
    private const string RuntimeLegacyHookMoonPowerAttribute = "HookMoonPower";

    private async ValueTask SendDueLegacyHookMoonExpirationsAsync(
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
                !TryExpireLegacyHookMoonStatusFlags(
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
                    $"WorldServer expired legacy HookMoon flags playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyHookMoonStatusFlags(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        var changed = false;
        removedVisibleFlag = false;
        updatedCharacter = character;

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyHookMoonStatusFlag,
                RuntimeLegacyHookMoonExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedHookMoonFlag,
                RuntimeLegacyHookMoonAttribute,
                RuntimeLegacyHookMoonActiveAttribute,
                RuntimeLegacyHookMoonAttackedPowerAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedHookMoonFlag;
        }

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyHookMoonCasterStatusFlag,
                RuntimeLegacyHookMoonCasterExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedHookMoonCasterFlag,
                RuntimeLegacyHookMoonCasterAttribute,
                RuntimeLegacyHookMoonPowerAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedHookMoonCasterFlag;
        }

        return changed;
    }

    private static bool TryExpireLegacyTimedStatusFlag(
        CharacterSnapshot character,
        DateTimeOffset now,
        int statusFlag,
        string expiresAtAttribute,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag,
        params string[] attributesToRemove)
    {
        updatedCharacter = character;
        removedVisibleFlag = false;
        if (!character.Attributes.TryGetValue(expiresAtAttribute, out var rawExpiresAt) ||
            !DateTimeOffset.TryParse(
                rawExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAt) ||
            expiresAt > now.ToUniversalTime())
        {
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(expiresAtAttribute);
        foreach (var attribute in attributesToRemove)
        {
            attributes.Remove(attribute);
        }

        removedVisibleFlag = RemoveLegacyStatusFlag(attributes, statusFlag);
        updatedCharacter = character with { Attributes = attributes };
        return true;
    }
}
