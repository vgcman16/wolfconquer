using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyBossStatusPublicationAttemptLimit = 4;

    private enum LegacyBossStatusPublicationOutcome
    {
        NotApplied,
        Applied,
        Deferred,
    }

    private readonly record struct LegacyBossStatusPublicationResult(
        WorldSession Session,
        LegacyBossStatusPublicationOutcome Outcome)
    {
        public bool Applied => Outcome == LegacyBossStatusPublicationOutcome.Applied;

        public bool Deferred => Outcome == LegacyBossStatusPublicationOutcome.Deferred;
    }

    internal static bool TryStartLegacyBossStatusLifetime(
        CharacterSnapshot character,
        LegacyBossStatusEffect effect,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter)
    {
        if (HasLegacyStatusFlag(character, effect.StatusFlag))
        {
            updatedCharacter = character;
            return false;
        }

        updatedCharacter = WithLegacyStatusFlag(character, effect.StatusFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            LegacyControlStatusLifetime.GetExpiresAtUtcAttribute(effect),
            now.ToUniversalTime()
                .AddSeconds(effect.DurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            LegacyControlStatusLifetime.GetRemoveOnDeadAttribute(effect),
            bool.TrueString);
        return true;
    }

    private static LegacyBossStatusPublicationResult TryPublishLegacyBossStatusLifetime(
        WorldSessionStore? sessions,
        ConquerTcpConnection subjectConnection,
        WorldSession initialSession,
        LegacyBossStatusEffect effect,
        bool bypassOppositeStatusGuard,
        DateTimeOffset now)
    {
        var expectedSession = initialSession;
        for (var attempt = 0; attempt < LegacyBossStatusPublicationAttemptLimit; attempt++)
        {
            var character = expectedSession.Character;
            if (HasLegacyStatusFlag(character, effect.StatusFlag) ||
                !bypassOppositeStatusGuard && HasLegacyStatusFlag(character, effect.BlockingFlag))
            {
                return new LegacyBossStatusPublicationResult(
                    expectedSession,
                    LegacyBossStatusPublicationOutcome.NotApplied);
            }

            if (!TryStartLegacyBossStatusLifetime(character, effect, now, out var updatedCharacter))
            {
                return new LegacyBossStatusPublicationResult(
                    expectedSession,
                    LegacyBossStatusPublicationOutcome.NotApplied);
            }

            var updatedSession = expectedSession with { Character = updatedCharacter };
            if (sessions is null ||
                sessions.TryCompareAndSet(subjectConnection, expectedSession, updatedSession))
            {
                return new LegacyBossStatusPublicationResult(
                    updatedSession,
                    LegacyBossStatusPublicationOutcome.Applied);
            }

            if (!sessions.TryGet(subjectConnection, out var currentSession) || currentSession is null)
            {
                return new LegacyBossStatusPublicationResult(
                    expectedSession,
                    LegacyBossStatusPublicationOutcome.Deferred);
            }

            expectedSession = currentSession;
        }

        return new LegacyBossStatusPublicationResult(
            expectedSession,
            LegacyBossStatusPublicationOutcome.Deferred);
    }
}
