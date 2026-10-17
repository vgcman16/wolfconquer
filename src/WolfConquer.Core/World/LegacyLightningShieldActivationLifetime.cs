using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>The owned237 registration clock; meter/powers remain independent.</summary>
public static class LegacyLightningShieldActivationLifetime
{
    public const int StatusFlag = 237;
    public const string ExpiresAtLocalAttribute = "LegacyLightningShieldActivatedExpiresAtLocal";

    public static CharacterSnapshot CaptureFirstInsertion(
        CharacterSnapshot before, CharacterSnapshot after, DateTime localNow)
    {
        if (LegacyPlayerDeathState.HasStatusFlag(before.Attributes, StatusFlag) ||
            !LegacyPlayerDeathState.HasStatusFlag(after.Attributes, StatusFlag)) return after;
        var attributes = new Dictionary<string, string>(after.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [ExpiresAtLocalAttribute] = DateTime.SpecifyKind(localNow, DateTimeKind.Unspecified)
                .AddSeconds(10).ToString("O", CultureInfo.InvariantCulture),
        };
        return after with { Attributes = attributes };
    }

    public static bool TryRead(CharacterSnapshot character, out DateTime expiresAtLocal)
    {
        expiresAtLocal = default;
        return character.Attributes.TryGetValue(ExpiresAtLocalAttribute, out var raw) &&
            DateTime.TryParseExact(raw, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out expiresAtLocal) &&
            expiresAtLocal.Kind == DateTimeKind.Unspecified &&
            expiresAtLocal.ToString("O", CultureInfo.InvariantCulture) == raw;
    }

    public static uint RemainingSeconds(CharacterSnapshot character, DateTime localNow)
    {
        // No invented timer for a historical/unmodeled already-active registration.
        if (!TryRead(character, out var expiresAtLocal)) return 10;
        // Source explicitly casts TotalSeconds; do not round up or clamp the wire value.
        return unchecked((uint)(expiresAtLocal - localNow).TotalSeconds);
    }

    public static CharacterSnapshot Forget(CharacterSnapshot character)
    {
        if (!character.Attributes.ContainsKey(ExpiresAtLocalAttribute)) return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        Forget(attributes);
        return character with { Attributes = attributes };
    }

    public static void Forget(Dictionary<string, string> attributes) => attributes.Remove(ExpiresAtLocalAttribute);
}
