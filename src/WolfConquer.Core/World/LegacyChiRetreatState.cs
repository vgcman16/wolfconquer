using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyChiRetreatState
{
    public static bool TryCancelRetreat(
        CharacterSnapshot character,
        LegacyChiPowerType powerType,
        out CharacterSnapshot updatedCharacter,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        updatedCharacter = character;

        if (!TryGetStorageKeys(powerType, out var timeKey, out var retreatedKey))
        {
            reason = "powerTypeInvalid";
            return false;
        }

        var hasRetreatTime = character.Attributes.TryGetValue(timeKey, out var rawTime) &&
            !string.IsNullOrWhiteSpace(rawTime) &&
            !string.Equals(rawTime, "0", StringComparison.Ordinal);
        var hasRetreatedPower = character.Attributes.TryGetValue(retreatedKey, out var retreatedPower) &&
            !string.IsNullOrWhiteSpace(retreatedPower);
        if (!hasRetreatTime && !hasRetreatedPower)
        {
            reason = "retreatMissing";
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [timeKey] = "0",
            [retreatedKey] = string.Empty,
        };

        updatedCharacter = character with { Attributes = attributes };
        reason = "cancelled";
        return true;
    }

    public static bool TrySealCurrentPower(
        CharacterSnapshot character,
        LegacyChiPowerType powerType,
        DateTime now,
        out CharacterSnapshot updatedCharacter,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        updatedCharacter = character;

        if (!TryGetStorageKeys(powerType, out var timeKey, out var retreatedKey))
        {
            reason = "powerTypeInvalid";
            return false;
        }

        if (!character.Attributes.TryGetValue(timeKey, out var rawTime) ||
            !long.TryParse(rawTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retreatTime) ||
            retreatTime == 0)
        {
            reason = "retreatMissing";
            return false;
        }

        if (retreatTime != 1)
        {
            DateTime expiresAt;
            try
            {
                expiresAt = DateTime.FromBinary(retreatTime);
            }
            catch (ArgumentException)
            {
                reason = "retreatTimeInvalid";
                return false;
            }

            if (now > expiresAt)
            {
                reason = "retreatExpired";
                return false;
            }
        }

        var power = LegacyChiState.FromSnapshot(character).Powers
            .FirstOrDefault(candidate => candidate.Type == powerType);
        if (power is null || !power.Unlocked)
        {
            reason = "powerLockedOrMissing";
            return false;
        }

        if (power.Attributes.Count != LegacyChiPowerState.AttributeCount)
        {
            reason = "powerAttributesInvalid";
            return false;
        }

        var retreatedPower = string.Join(
            '|',
            power.Attributes.Select(attribute =>
                attribute.Type.ToString(CultureInfo.InvariantCulture) + "-" +
                attribute.Value.ToString(CultureInfo.InvariantCulture)));
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [retreatedKey] = retreatedPower,
        };

        updatedCharacter = character with { Attributes = attributes };
        reason = "sealed";
        return true;
    }

    public static bool TryRestoreRetreatedPower(
        CharacterSnapshot character,
        LegacyChiPowerType powerType,
        DateTime now,
        out CharacterSnapshot updatedCharacter,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        updatedCharacter = character;

        if (!TryGetStorageKeys(powerType, out var timeKey, out var retreatedKey))
        {
            reason = "powerTypeInvalid";
            return false;
        }

        if (!character.Attributes.TryGetValue(timeKey, out var rawTime) ||
            string.IsNullOrWhiteSpace(rawTime) ||
            string.Equals(rawTime, "0", StringComparison.Ordinal))
        {
            reason = "retreatMissing";
            return false;
        }

        if (!long.TryParse(rawTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retreatTime))
        {
            reason = "retreatTimeInvalid";
            return false;
        }

        if (retreatTime != 1)
        {
            DateTime expiresAt;
            try
            {
                expiresAt = DateTime.FromBinary(retreatTime);
            }
            catch (ArgumentException)
            {
                reason = "retreatTimeInvalid";
                return false;
            }

            if (now > expiresAt)
            {
                reason = "retreatExpired";
                return false;
            }
        }

        if (!character.Attributes.TryGetValue(retreatedKey, out var rawRetreatedPower) ||
            string.IsNullOrWhiteSpace(rawRetreatedPower))
        {
            reason = "retreatedPowerMissing";
            return false;
        }

        if (!TryParseRetreatedPower(rawRetreatedPower, out var retreatedAttributes))
        {
            reason = "retreatedPowerInvalid";
            return false;
        }

        if (!TryGetCurrentPower(character, powerType, out var powerKey, out var currentPower))
        {
            reason = "powerLockedOrMissing";
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [powerKey] = SerializePower(currentPower.Experience, retreatedAttributes),
        };

        updatedCharacter = character with { Attributes = attributes };
        reason = "restored";
        return true;
    }

    private static bool TryGetCurrentPower(
        CharacterSnapshot character,
        LegacyChiPowerType powerType,
        out string powerKey,
        out LegacyChiPowerState power)
    {
        var keys = powerType switch
        {
            LegacyChiPowerType.Dragon => new[] { "Dragon" },
            LegacyChiPowerType.Phoenix => new[] { "Pheonix", "Phoenix" },
            LegacyChiPowerType.Tiger => new[] { "Tiger" },
            LegacyChiPowerType.Turtle => new[] { "Turtle" },
            _ => [],
        };

        foreach (var key in keys)
        {
            if (!character.Attributes.TryGetValue(key, out var rawPower))
            {
                continue;
            }

            var parsed = LegacyChiPowerState.Parse(powerType, rawPower);
            if (parsed.Unlocked)
            {
                powerKey = key;
                power = parsed;
                return true;
            }

            break;
        }

        powerKey = string.Empty;
        power = null!;
        return false;
    }

    private static bool TryParseRetreatedPower(
        string rawRetreatedPower,
        out IReadOnlyList<LegacyChiAttributeState> attributes)
    {
        var entries = rawRetreatedPower.Split('|', StringSplitOptions.None);
        if (entries.Length != LegacyChiPowerState.AttributeCount)
        {
            attributes = [];
            return false;
        }

        var parsed = new LegacyChiAttributeState[LegacyChiPowerState.AttributeCount];
        for (var index = 0; index < entries.Length; index++)
        {
            var fields = entries[index].Split('-', StringSplitOptions.None);
            if (fields.Length != 2 ||
                !byte.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                attributes = [];
                return false;
            }

            parsed[index] = new LegacyChiAttributeState(type, value, Locked: false);
        }

        attributes = parsed;
        return true;
    }

    private static string SerializePower(
        uint experience,
        IReadOnlyList<LegacyChiAttributeState> attributes)
    {
        var fields = new List<string>(2 + (LegacyChiPowerState.AttributeCount * 3))
        {
            "1",
            experience.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var attribute in attributes)
        {
            fields.Add(attribute.Type.ToString(CultureInfo.InvariantCulture));
            fields.Add(attribute.Value.ToString(CultureInfo.InvariantCulture));
            fields.Add("0");
        }

        return string.Join('/', fields) + "/";
    }

    private static bool TryGetStorageKeys(
        LegacyChiPowerType powerType,
        out string timeKey,
        out string retreatedKey)
    {
        (timeKey, retreatedKey) = powerType switch
        {
            LegacyChiPowerType.Dragon => ("DragonTime", "RetreatedDragon"),
            LegacyChiPowerType.Phoenix => ("PhoenixTime", "RetreatedPhoenix"),
            LegacyChiPowerType.Tiger => ("TigerTime", "RetreatedTiger"),
            LegacyChiPowerType.Turtle => ("TurtleTime", "RetreatedTurtle"),
            _ => (string.Empty, string.Empty),
        };
        return timeKey.Length > 0;
    }
}
