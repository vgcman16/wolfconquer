using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyInvisibilityAttachStatusSpellId = 1_075;
    private const ushort LegacyStarOfAccuracyAttachStatusSpellId = 1_085;
    private const ushort LegacyMagicShieldAttachStatusSpellId = 1_090;
    private const ushort LegacyStigmaAttachStatusSpellId = 1_095;

    internal const string RuntimeLegacyInvisibilityExpiresAtUtcAttribute =
        "LegacyInvisibilityExpiresAtUtc";
    internal const string RuntimeLegacyInvisibilityRemoveOnDeadAttribute =
        "LegacyInvisibilityRemoveOnDead";

    private static WorldSession ApplyLegacyClassicBuffToPlayer(
        WorldSession session,
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute,
        uint durationSeconds,
        DateTimeOffset now)
    {
        var character = WithLegacyStatusFlag(
            LegacyRootItemEffectFlagClock.ForgetProjection(session.Character, statusFlag), statusFlag);
        character = WithCharacterAttribute(
            character,
            expiresAtAttribute,
            now.ToUniversalTime()
                .AddSeconds(durationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        character = WithCharacterAttribute(
            character,
            removeOnDeadAttribute,
            bool.TrueString);
        return session with { Character = character };
    }

    private static bool TryGetLegacyClassicBuffDefinition(
        ushort spellId,
        out int statusFlag,
        out string expiresAtAttribute,
        out string removeOnDeadAttribute,
        out bool suppressWhenActive)
    {
        (statusFlag, expiresAtAttribute, removeOnDeadAttribute, suppressWhenActive) = spellId switch
        {
            LegacyInvisibilityAttachStatusSpellId =>
                (LegacyInvisibilityFlag,
                    RuntimeLegacyInvisibilityExpiresAtUtcAttribute,
                    RuntimeLegacyInvisibilityRemoveOnDeadAttribute,
                    false),
            LegacyStarOfAccuracyAttachStatusSpellId =>
                (LegacyStarOfAccuracyFlag,
                    RuntimeLegacyAccuracyExpiresAtUtcAttribute,
                    RuntimeLegacyAccuracyRemoveOnDeadAttribute,
                    false),
            LegacyMagicShieldAttachStatusSpellId =>
                (LegacyMagicShieldFlag,
                    RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
                    RuntimeLegacyMagicShieldRemoveOnDeadAttribute,
                    true),
            LegacyStigmaAttachStatusSpellId =>
                (LegacyStigmaFlag,
                    RuntimeLegacyStigmaExpiresAtUtcAttribute,
                    RuntimeLegacyStigmaRemoveOnDeadAttribute,
                    false),
            _ => (-1, string.Empty, string.Empty, false),
        };

        return statusFlag >= 0;
    }
}
