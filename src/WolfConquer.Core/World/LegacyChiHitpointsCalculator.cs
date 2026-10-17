using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyChiHitpointsContribution(
    uint RolledHitpoints,
    uint FateHitpoints,
    uint DragonScoreBonusHitpoints,
    uint Total,
    IReadOnlyList<string> MissingInputs);

public static class LegacyChiHitpointsCalculator
{
    private const byte HpAddAttributeType = (byte)LegacyChiAttributeType.HPAdd;

    public static LegacyChiHitpointsContribution Calculate(
        CharacterSnapshot snapshot,
        WorldDataCatalog? catalog = null,
        uint percent = 100)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Calculate(LegacyChiState.FromSnapshot(snapshot), catalog?.FateExperienceByType, percent);
    }

    public static LegacyChiHitpointsContribution Calculate(
        LegacyChiState state,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType = null,
        uint percent = 100)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Legacy Chi HP percent cannot exceed 100.");
        }

        var rolledHitpoints = 0u;
        var fateHitpoints = 0u;
        var dragonScoreBonusHitpoints = 0u;
        var missingInputs = new List<string>();

        foreach (var power in state.Powers)
        {
            if (!power.Unlocked)
            {
                continue;
            }

            rolledHitpoints = checked(rolledHitpoints + SumHpAdd(power.Attributes));

            var fate = FindFateAttributes(power, fateExperienceByType);
            if (fate is null)
            {
                if (fateExperienceByType is null)
                {
                    AddMissing(missingInputs, "Chi.FateExperience");
                }
            }
            else
            {
                fateHitpoints = checked(fateHitpoints + SumEncodedHpAdd(fate.Values));
            }

            if (power.Type == LegacyChiPowerType.Dragon)
            {
                if (CalculateScore(power) == 400)
                {
                    dragonScoreBonusHitpoints = checked(dragonScoreBonusHitpoints + 5_000u);
                }
                else
                {
                    AddMissing(missingInputs, "Chi.DragonRank");
                }
            }
        }

        var rawTotal = checked(rolledHitpoints + fateHitpoints + dragonScoreBonusHitpoints);
        var total = checked((uint)(((ulong)rawTotal * percent) / 100u));
        return new LegacyChiHitpointsContribution(
            rolledHitpoints,
            fateHitpoints,
            dragonScoreBonusHitpoints,
            total,
            missingInputs);
    }

    private static FateExperience? FindFateAttributes(
        LegacyChiPowerState power,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType)
    {
        if (fateExperienceByType is null ||
            !fateExperienceByType.TryGetValue((int)power.Type, out var candidates))
        {
            return null;
        }

        return candidates
            .Where(entry => power.Experience >= 0 && power.Experience >= (ulong)Math.Max(0, entry.Experience))
            .OrderBy(entry => entry.Experience)
            .LastOrDefault();
    }

    private static uint SumHpAdd(IEnumerable<LegacyChiAttributeState> attributes)
    {
        var sum = 0u;
        foreach (var attribute in attributes)
        {
            if (attribute.Type == HpAddAttributeType)
            {
                sum = checked(sum + attribute.Value);
            }
        }

        return sum;
    }

    private static uint SumEncodedHpAdd(IEnumerable<long> encodedAttributes)
    {
        var sum = 0u;
        foreach (var encoded in encodedAttributes)
        {
            if (encoded < 0)
            {
                continue;
            }

            var type = encoded / 10_000;
            var value = encoded % 10_000;
            if (type == HpAddAttributeType)
            {
                sum = checked(sum + (uint)value);
            }
        }

        return sum;
    }

    private static int CalculateScore(LegacyChiPowerState power)
    {
        var score = 0;
        foreach (var attribute in power.Attributes)
        {
            var max = ChiMaxValue(attribute.Type);
            var min = ChiMinValue(attribute.Type);
            if (max <= min)
            {
                continue;
            }

            var boundedValue = Math.Clamp(attribute.Value, min, max);
            score += (int)((uint)(boundedValue - min) / ((double)(max - min) / 100.0));
        }

        return score;
    }

    private static int ChiMaxValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
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
            LegacyChiAttributeType.PhysicalDamageDecrease => 500,
            LegacyChiAttributeType.MagicDamageIncrease => 300,
            LegacyChiAttributeType.MagicDamageDecrease => 300,
            _ => 100,
        };
    }

    private static int ChiMinValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 10,
            LegacyChiAttributeType.SkillCriticalStrike => 10,
            LegacyChiAttributeType.Immunity => 10,
            LegacyChiAttributeType.Breakthrough => 10,
            LegacyChiAttributeType.Counteraction => 10,
            LegacyChiAttributeType.HPAdd => 1_000,
            LegacyChiAttributeType.AddAttack => 500,
            LegacyChiAttributeType.AddMagicAttack => 500,
            LegacyChiAttributeType.AddMagicDefense => 50,
            LegacyChiAttributeType.PhysicalDamageIncrease => 50,
            LegacyChiAttributeType.PhysicalDamageDecrease => 50,
            LegacyChiAttributeType.MagicDamageIncrease => 50,
            LegacyChiAttributeType.MagicDamageDecrease => 50,
            _ => 10,
        };
    }

    private static void AddMissing(ICollection<string> missingInputs, string value)
    {
        if (!missingInputs.Contains(value))
        {
            missingInputs.Add(value);
        }
    }
}
