using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyAttackStampUtcAttribute = "LegacyAttackStampUtc";
    private const string RuntimeLegacyProtectExpiresAtUtcAttribute = "LegacyProtectExpiresAtUtc";

    private static CharacterSnapshot WithLegacyPlayerAttackProtection(
        CharacterSnapshot character,
        DateTimeOffset now,
        TimeSpan duration)
    {
        var expiresAt = now.ToUniversalTime() + duration;
        var formattedExpiresAt = expiresAt.ToString("O", CultureInfo.InvariantCulture);
        return WithCharacterAttribute(
            WithCharacterAttribute(character, RuntimeLegacyAttackStampUtcAttribute, formattedExpiresAt),
            RuntimeLegacyProtectExpiresAtUtcAttribute,
            formattedExpiresAt);
    }

    private static bool IsLegacyPlayerAttackProtected(CharacterSnapshot character, DateTimeOffset now)
    {
        return TryReadLegacyDateTimeOffsetAttribute(
                character.Attributes,
                RuntimeLegacyProtectExpiresAtUtcAttribute,
                out var protectExpiresAt) &&
            now.ToUniversalTime() < protectExpiresAt.ToUniversalTime();
    }

    private static CharacterSnapshot WithLegacyPlayerAttackStamp(
        CharacterSnapshot character,
        DateTimeOffset now)
    {
        return WithCharacterAttribute(
            character,
            RuntimeLegacyAttackStampUtcAttribute,
            now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static bool TryReadLegacyPlayerAttackStamp(
        CharacterSnapshot character,
        out DateTimeOffset attackStamp)
    {
        return TryReadLegacyDateTimeOffsetAttribute(
            character.Attributes,
            RuntimeLegacyAttackStampUtcAttribute,
            out attackStamp);
    }

    private static bool TryReadLegacyDateTimeOffsetAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out DateTimeOffset value)
    {
        value = default;
        return attributes.TryGetValue(key, out var raw) &&
            DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value);
    }
}
