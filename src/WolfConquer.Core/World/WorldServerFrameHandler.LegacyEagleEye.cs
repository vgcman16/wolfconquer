using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyEagleEyeCountdownExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var playerCount = 0;
        foreach (var active in _sessions.Snapshot())
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyEagleEyeCountdown(currentSession.Character, now, out var expiredCharacter))
            {
                continue;
            }

            _sessions.Set(active.Connection, currentSession with { Character = expiredCharacter });
            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer expired legacy EagleEye countdown playerCount={playerCount}; " +
                    "source=ThreadPool.cs EagleEyeStamp+20s.")
                .ConfigureAwait(false);
        }
    }

    internal static bool TryExpireLegacyEagleEyeCountdown(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter)
    {
        updatedCharacter = character;
        if (!IsLegacyEagleEyeCountdownActive(character) ||
            !character.Attributes.TryGetValue(
                WorldPacketHandlers.RuntimeLegacyEagleEyeExpiresAtUtcAttribute,
                out var rawExpiresAt) ||
            !DateTimeOffset.TryParse(
                rawExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAt) ||
            expiresAt > now)
        {
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove("EagleEyeCountDown");
        attributes.Remove("EagleEyeCountdown");
        attributes.Remove(WorldPacketHandlers.RuntimeLegacyEagleEyeExpiresAtUtcAttribute);
        attributes[WorldPacketHandlers.RuntimeLegacyEagleEyeCountDownAttribute] = "0";
        updatedCharacter = character with { Attributes = attributes };
        return true;
    }

    private static bool IsLegacyEagleEyeCountdownActive(CharacterSnapshot character)
    {
        foreach (var key in new[]
                 {
                     WorldPacketHandlers.RuntimeLegacyEagleEyeCountDownAttribute,
                     "EagleEyeCountDown",
                     "EagleEyeCountdown",
                 })
        {
            if (character.Attributes.TryGetValue(key, out var raw) &&
                (raw == "1" || bool.TryParse(raw, out var active) && active))
            {
                return true;
            }
        }

        return false;
    }
}
