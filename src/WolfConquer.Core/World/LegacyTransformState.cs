using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyTransformState
{
    public const string TransformationIdAttribute = "TransformationID";
    public const string TransformationIdAliasAttribute = "TransformationId";
    public const string ActiveAttribute = "LegacyTransformInfoActive";
    public const string ActiveAliasAttribute = "TransformInfoActive";
    public const string ExpiresAtUtcAttribute = "LegacyTransformExpiresAtUtc";
    public const string ExpiresAtUtcAliasAttribute = "TransformExpiresAtUtc";
    public const string TransformationExpiresAtUtcAliasAttribute = "TransformationExpiresAtUtc";
    public const string MaxHitpointsAttribute = "LegacyTransformMaxHitpoints";
    public const string MaxHitpointsAliasAttribute = "TransformMaxHitpoints";

    private static readonly string[] TransformationIdAttributes =
    [
        TransformationIdAttribute,
        TransformationIdAliasAttribute,
    ];

    private static readonly string[] ActiveAttributes =
    [
        ActiveAttribute,
        ActiveAliasAttribute,
    ];

    private static readonly string[] ExpiresAtUtcAttributes =
    [
        ExpiresAtUtcAttribute,
        ExpiresAtUtcAliasAttribute,
        TransformationExpiresAtUtcAliasAttribute,
    ];

    private static readonly string[] MaxHitpointsAttributes =
    [
        MaxHitpointsAttribute,
        MaxHitpointsAliasAttribute,
    ];

    public static CharacterSnapshot Apply(
        CharacterSnapshot character,
        uint lookface,
        uint maxHitpoints,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (lookface == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lookface));
        }

        if (maxHitpoints == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHitpoints));
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        var lookfaceValue = lookface.ToString(CultureInfo.InvariantCulture);
        var maxHitpointsValue = maxHitpoints.ToString(CultureInfo.InvariantCulture);
        var expiresAtValue = expiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        attributes[TransformationIdAttribute] = lookfaceValue;
        attributes[TransformationIdAliasAttribute] = lookfaceValue;
        attributes[ActiveAttribute] = "1";
        attributes[ActiveAliasAttribute] = "1";
        attributes[ExpiresAtUtcAttribute] = expiresAtValue;
        attributes[ExpiresAtUtcAliasAttribute] = expiresAtValue;
        attributes[TransformationExpiresAtUtcAliasAttribute] = expiresAtValue;
        attributes[MaxHitpointsAttribute] = maxHitpointsValue;
        attributes[MaxHitpointsAliasAttribute] = maxHitpointsValue;

        return character with
        {
            MinHitPoints = maxHitpoints,
            Attributes = attributes,
        };
    }

    public static bool TryGetActiveMaxHitpoints(
        CharacterSnapshot character,
        out uint maxHitpoints)
    {
        ArgumentNullException.ThrowIfNull(character);
        maxHitpoints = 0;
        if (!TryReadUInt32(character.Attributes, TransformationIdAttributes, out var transformationId) ||
            transformationId == 0 ||
            IsExplicitlyInactive(character.Attributes))
        {
            return false;
        }

        return TryReadUInt32(character.Attributes, MaxHitpointsAttributes, out maxHitpoints) &&
            maxHitpoints != 0;
    }

    public static bool IsExpired(CharacterSnapshot character, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!TryReadUInt32(character.Attributes, TransformationIdAttributes, out var transformationId) ||
            transformationId == 0 ||
            IsExplicitlyInactive(character.Attributes) ||
            !TryReadDateTimeOffset(character.Attributes, ExpiresAtUtcAttributes, out var expiresAtUtc))
        {
            return false;
        }

        return now.ToUniversalTime() > expiresAtUtc;
    }

    public static bool TryClear(
        CharacterSnapshot character,
        out CharacterSnapshot clearedCharacter)
    {
        ArgumentNullException.ThrowIfNull(character);
        clearedCharacter = character;
        if (!TryReadUInt32(character.Attributes, TransformationIdAttributes, out var transformationId) ||
            transformationId == 0 ||
            IsExplicitlyInactive(character.Attributes))
        {
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [TransformationIdAttribute] = "0",
            [TransformationIdAliasAttribute] = "0",
        };
        foreach (var key in ActiveAttributes)
        {
            attributes.Remove(key);
        }

        foreach (var key in ExpiresAtUtcAttributes)
        {
            attributes.Remove(key);
        }

        foreach (var key in MaxHitpointsAttributes)
        {
            attributes.Remove(key);
        }

        clearedCharacter = character with { Attributes = attributes };
        return true;
    }

    public static CharacterSnapshot NormalizeAfterClear(
        WorldSession session,
        CharacterSnapshot clearedCharacter,
        WorldDataCatalog? worldData,
        out uint maxHitpoints)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clearedCharacter);

        maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            clearedCharacter,
            currentHitpoints: 0,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        var currentHitpoints = clearedCharacter.MinHitPoints <= 0
            ? 0u
            : clearedCharacter.MinHitPoints >= maxHitpoints
                ? maxHitpoints
                : (uint)clearedCharacter.MinHitPoints;

        return clearedCharacter with { MinHitPoints = currentHitpoints };
    }

    private static bool IsExplicitlyInactive(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in ActiveAttributes)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (bool.TryParse(raw, out var active))
            {
                return !active;
            }

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                return numeric == 0;
            }
        }

        return false;
    }

    private static bool TryReadUInt32(
        IReadOnlyDictionary<string, string> attributes,
        IEnumerable<string> keys,
        out uint value)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryReadDateTimeOffset(
        IReadOnlyDictionary<string, string> attributes,
        IEnumerable<string> keys,
        out DateTimeOffset value)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }
}
