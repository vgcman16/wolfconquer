using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyChiStudyMutation(
    CharacterSnapshot Character,
    int PointsSpent,
    IReadOnlyList<LegacyChiStudyScoreNotification> ScoreNotifications);

public sealed record LegacyChiStudyScoreNotification(
    LegacyChiPowerType PowerType,
    uint AttributeScore,
    string CharacterName);

public static class LegacyChiStudyState
{
    private const byte MaximumLockedFlags = 0x0F;
    private const int BaseStudyCost = 50;
    private const uint OverflowExperienceStep = 100_000;
    private const int OverflowChiPoints = 20_000;
    private const int MaximumRerollAttempts = 1_024;

    public static bool TryStudy(
        CharacterSnapshot character,
        LegacyChiPowerType powerType,
        byte lockedFlags,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType,
        Func<int, int, int>? randomNextInclusive,
        out LegacyChiStudyMutation mutation,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        mutation = new LegacyChiStudyMutation(character, PointsSpent: 0, ScoreNotifications: []);

        if (powerType is < LegacyChiPowerType.Dragon or > LegacyChiPowerType.Turtle)
        {
            reason = "powerTypeInvalid";
            return false;
        }

        if ((lockedFlags & ~MaximumLockedFlags) != 0)
        {
            reason = "lockedFlagsInvalid";
            return false;
        }

        if (!TryGetCurrentPower(character, powerType, out var powerKey, out var power) ||
            !power.Unlocked)
        {
            reason = "powerLockedOrMissing";
            return false;
        }

        if (character.Level < 110 && character.Reborn == 0)
        {
            reason = "levelOrRebornTooLow";
            return false;
        }

        if (power.Attributes.Count != LegacyChiPowerState.AttributeCount)
        {
            reason = "powerAttributesInvalid";
            return false;
        }

        if (fateExperienceByType is null ||
            !fateExperienceByType.TryGetValue((int)powerType, out var fateRows) ||
            fateRows.Count == 0)
        {
            reason = "fateExperienceMissing";
            return false;
        }

        var level98 = fateRows.FirstOrDefault(row => row.Level == 98);
        if (level98 is null || level98.Experience is < 0 or > uint.MaxValue)
        {
            reason = "level98ExperienceMissing";
            return false;
        }

        var cost = BaseStudyCost + (CountLockedBits(lockedFlags) * BaseStudyCost);
        var currentFate = fateRows
            .Where(row => row.Experience >= 0 && (ulong)row.Experience <= power.Experience)
            .OrderBy(row => row.Experience)
            .LastOrDefault();
        if (currentFate is not null)
        {
            if (currentFate.Values.Count <= 6 || currentFate.Values[6] < 0)
            {
                reason = "costReductionInvalid";
                return false;
            }

            var reductionPercent = currentFate.Values[6] / 100;
            if (reductionPercent > 100)
            {
                reason = "costReductionInvalid";
                return false;
            }

            cost -= (int)(cost * (reductionPercent / 100.0));
        }

        var chiPoints = TryReadInt32(character.Attributes, "ChiPoints", out var parsedPoints)
            ? parsedPoints
            : 0;
        if (chiPoints < cost)
        {
            reason = "chiPointsInsufficient";
            return false;
        }

        chiPoints -= cost;
        var experience = unchecked(power.Experience + (uint)cost);
        if (TryReadInt32(character.Attributes, "ChargeChi", out var chargeChi) && chargeChi == 1)
        {
            experience = (uint)level98.Experience;
        }

        var overflowThreshold = checked((uint)level98.Experience + OverflowExperienceStep);
        while (experience >= overflowThreshold)
        {
            experience -= OverflowExperienceStep;
            chiPoints = checked(chiPoints + OverflowChiPoints);
        }

        if (!TryRerollAttributes(
                power.Attributes,
                powerType,
                character.Name,
                lockedFlags,
                randomNextInclusive,
                out var attributes,
                out var scoreNotifications))
        {
            reason = "rerollExhausted";
            return false;
        }

        var updatedAttributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ChiPoints"] = chiPoints.ToString(CultureInfo.InvariantCulture),
            [powerKey] = SerializePower(experience, attributes),
        };
        mutation = new LegacyChiStudyMutation(
            character with { Attributes = updatedAttributes },
            cost,
            scoreNotifications);
        reason = "studied";
        return true;
    }

    private static bool TryRerollAttributes(
        IReadOnlyList<LegacyChiAttributeState> current,
        LegacyChiPowerType powerType,
        string characterName,
        byte lockedFlags,
        Func<int, int, int>? randomNextInclusive,
        out IReadOnlyList<LegacyChiAttributeState> attributes,
        out IReadOnlyList<LegacyChiStudyScoreNotification> scoreNotifications)
    {
        var rerolled = current.ToArray();
        var notifications = new List<LegacyChiStudyScoreNotification>();
        for (var index = 0; index < rerolled.Length; index++)
        {
            if ((lockedFlags & (1 << index)) != 0)
            {
                rerolled[index] = rerolled[index] with { Locked = true };
                continue;
            }

            if (!TryNextAttributeType(randomNextInclusive, out _))
            {
                attributes = [];
                scoreNotifications = [];
                return false;
            }

            var accepted = false;
            for (var attempt = 0; attempt < MaximumRerollAttempts; attempt++)
            {
                if (!TryNextAttributeType(randomNextInclusive, out var candidate))
                {
                    attributes = [];
                    scoreNotifications = [];
                    return false;
                }

                var duplicate = false;
                for (var otherIndex = 0; otherIndex < rerolled.Length; otherIndex++)
                {
                    if (otherIndex != index && rerolled[otherIndex].Type == candidate)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                var maximumValue = MaximumValue((LegacyChiAttributeType)candidate);
                rerolled[index] = new LegacyChiAttributeState(
                    checked((byte)candidate),
                    checked((ushort)maximumValue),
                    Locked: false);
                // Source StatsRand defaults to epic mode, so the maximum always crosses its 90% broadcast gate.
                notifications.Add(new LegacyChiStudyScoreNotification(
                    powerType,
                    checked((uint)((candidate * 10_000) + maximumValue)),
                    characterName));

                accepted = true;
                break;
            }

            if (!accepted)
            {
                attributes = [];
                scoreNotifications = [];
                return false;
            }
        }

        attributes = rerolled;
        scoreNotifications = notifications;
        return true;
    }

    private static bool TryNextAttributeType(
        Func<int, int, int>? randomNextInclusive,
        out int attributeType)
    {
        attributeType = randomNextInclusive is null
            ? Random.Shared.Next(1, 14)
            : randomNextInclusive(1, 13);
        return attributeType is >= 1 and <= 13;
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
            if (character.Attributes.TryGetValue(key, out var rawPower))
            {
                powerKey = key;
                power = LegacyChiPowerState.Parse(powerType, rawPower);
                return true;
            }
        }

        powerKey = string.Empty;
        power = null!;
        return false;
    }

    private static int CountLockedBits(byte lockedFlags)
    {
        var count = 0;
        for (var bit = 0; bit < LegacyChiPowerState.AttributeCount; bit++)
        {
            if ((lockedFlags & (1 << bit)) != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int MaximumValue(LegacyChiAttributeType attributeType)
    {
        return attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 200,
            LegacyChiAttributeType.SkillCriticalStrike => 200,
            LegacyChiAttributeType.Immunity => 200,
            LegacyChiAttributeType.Breakthrough => 200,
            LegacyChiAttributeType.Counteraction => 200,
            LegacyChiAttributeType.HPAdd => 3_500,
            LegacyChiAttributeType.AddAttack => 2_000,
            LegacyChiAttributeType.AddMagicAttack => 2_500,
            LegacyChiAttributeType.AddMagicDefense => 250,
            LegacyChiAttributeType.PhysicalDamageIncrease => 500,
            LegacyChiAttributeType.MagicDamageIncrease => 300,
            LegacyChiAttributeType.PhysicalDamageDecrease => 500,
            LegacyChiAttributeType.MagicDamageDecrease => 300,
            _ => 100,
        };
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
            fields.Add(attribute.Locked ? "1" : "0");
        }

        return string.Join('/', fields) + "/";
    }

    private static bool TryReadInt32(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out int value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var rawValue) &&
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
