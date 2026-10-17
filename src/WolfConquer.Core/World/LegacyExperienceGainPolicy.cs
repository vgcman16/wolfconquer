using System.Globalization;

namespace WolfConquer.Core.World;

public readonly record struct LegacyExperienceGainInputs(
    double BaseAmount,
    int PlayerLevel,
    int CursedTimer,
    bool SoulChantEquipped,
    byte SoulChantItemLevel,
    double ServerExperienceRate,
    int RainbowGemPercent,
    int DoubleExperienceTime,
    double DoubleExperienceRate,
    int MapId,
    bool HasOblivion,
    bool HasHeavenBlessing);

public enum LegacyExperienceGainSuppressionReason
{
    None = 0,
    ZeroDamage = 1,
    Cursed = 2,
    MaxLevel = 3,
    SoulChantReducedToZero = 4,
}

public readonly record struct LegacyExperienceGainResult(
    ulong AppliedExperienceGain,
    ulong OblivionExperienceGain,
    uint HuntingBlessingGain,
    LegacyExperienceGainSuppressionReason SuppressionReason)
{
    public bool Suppressed => SuppressionReason != LegacyExperienceGainSuppressionReason.None;

    public bool RedirectedToOblivion => OblivionExperienceGain != 0;

    public bool GrantsHuntingBlessing => HuntingBlessingGain != 0;
}

public static class LegacyExperienceGainPolicy
{
    public const string UserExperienceRateAttributeName = "UserExpRate";
    public const string ServerExperienceRateAttributeName = "ServerExperienceRate";
    public const string ExperienceRateAttributeName = "ExperienceRate";
    public const string MonsterKillExperienceRateAttributeName = "MonsterKillExperienceRate";
    public const string MaximumServerExperienceRateAttributeName = "MaximumServerExperienceRate";
    public const string MaximumMonsterKillExperienceRateAttributeName = "MaximumMonsterKillExperienceRate";
    public const string UserExperienceRateEnvironmentVariableName = "WOLFCONQUER_USER_EXP_RATE";
    public const string MonsterExperienceRateEnvironmentVariableName = "WOLFCONQUER_MONSTER_EXP_RATE";
    public const string MaximumMonsterExperienceRateEnvironmentVariableName = "WOLFCONQUER_MAX_MONSTER_EXP_RATE";
    public const double DefaultMaximumServerExperienceRate = 10.0;
    public const int MaximumExperienceLevel = 140;
    public const int CursedTimerSuppressionThreshold = 2;
    public const int SoulChantBaseReductionPercent = 10;
    public const int SoulChantItemLevelReductionPercent = 10;
    public const int HundredPercent = 100;
    public const int Map1039ExperienceDivisor = 100;
    public const int HeavenBlessingHuntingDivisor = 10;
    public const int OblivionExperienceMultiplier = 4;
    public const int ReducedExperienceMapId = 1039;

    public static double ReadServerExperienceRate(
        IReadOnlyDictionary<string, string> attributes,
        Func<string, string?>? readEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        if (TryReadDoubleAttribute(
            attributes,
            out var rate,
            UserExperienceRateAttributeName,
            ServerExperienceRateAttributeName,
            ExperienceRateAttributeName,
            MonsterKillExperienceRateAttributeName))
        {
            return ApplyConfiguredMaximumServerExperienceRate(attributes, readEnvironmentVariable, rate);
        }

        return TryReadEnvironmentRate(readEnvironmentVariable, UserExperienceRateEnvironmentVariableName, out rate) ||
            TryReadEnvironmentRate(readEnvironmentVariable, MonsterExperienceRateEnvironmentVariableName, out rate)
                ? ApplyConfiguredMaximumServerExperienceRate(attributes, readEnvironmentVariable, rate)
                : ApplyConfiguredMaximumServerExperienceRate(attributes, readEnvironmentVariable, 1.0);
    }

    public static LegacyExperienceGainResult CalculateAttackUpdate(LegacyExperienceGainInputs inputs)
    {
        ValidateInputs(inputs);

        if (inputs.BaseAmount == 0)
        {
            return Suppressed(LegacyExperienceGainSuppressionReason.ZeroDamage);
        }

        var damage = ToLegacyDamage(inputs.BaseAmount);
        var huntingBlessingGain = CalculateHuntingBlessingGain(damage, inputs.HasHeavenBlessing);

        if (inputs.HasOblivion)
        {
            return new LegacyExperienceGainResult(
                AppliedExperienceGain: 0,
                OblivionExperienceGain: CalculateOblivionExperienceGain(damage),
                huntingBlessingGain,
                LegacyExperienceGainSuppressionReason.None);
        }

        var experienceResult = CalculateIncreaseExperience(inputs);
        return experienceResult with
        {
            HuntingBlessingGain = huntingBlessingGain,
        };
    }

    public static LegacyExperienceGainResult CalculateIncreaseExperience(LegacyExperienceGainInputs inputs)
    {
        ValidateInputs(inputs);

        if (inputs.CursedTimer > CursedTimerSuppressionThreshold)
        {
            return Suppressed(LegacyExperienceGainSuppressionReason.Cursed);
        }

        if (inputs.PlayerLevel >= MaximumExperienceLevel)
        {
            return Suppressed(LegacyExperienceGainSuppressionReason.MaxLevel);
        }

        var experience = inputs.BaseAmount;
        if (inputs.SoulChantEquipped)
        {
            var soulChantReductionPercent =
                SoulChantBaseReductionPercent + inputs.SoulChantItemLevel * SoulChantItemLevelReductionPercent;
            experience -= experience * soulChantReductionPercent / HundredPercent;
            if (experience == 0)
            {
                return Suppressed(LegacyExperienceGainSuppressionReason.SoulChantReducedToZero);
            }
        }

        experience *= inputs.ServerExperienceRate;
        experience += experience * inputs.RainbowGemPercent / HundredPercent;

        if (inputs.DoubleExperienceTime != 0)
        {
            experience *= inputs.DoubleExperienceRate;
        }

        if (inputs.MapId == ReducedExperienceMapId)
        {
            experience /= Map1039ExperienceDivisor;
        }

        return new LegacyExperienceGainResult(
            (ulong)experience,
            OblivionExperienceGain: 0,
            HuntingBlessingGain: 0,
            LegacyExperienceGainSuppressionReason.None);
    }

    private static LegacyExperienceGainResult Suppressed(LegacyExperienceGainSuppressionReason reason)
    {
        return new LegacyExperienceGainResult(
            AppliedExperienceGain: 0,
            OblivionExperienceGain: 0,
            HuntingBlessingGain: 0,
            reason);
    }

    private static ulong CalculateOblivionExperienceGain(uint damage)
    {
        return unchecked((ulong)(damage * OblivionExperienceMultiplier));
    }

    private static uint CalculateHuntingBlessingGain(uint damage, bool hasHeavenBlessing)
    {
        return hasHeavenBlessing ? damage / HeavenBlessingHuntingDivisor : 0;
    }

    private static uint ToLegacyDamage(double baseAmount)
    {
        return (uint)baseAmount;
    }

    private static bool TryReadEnvironmentRate(
        Func<string, string?> readEnvironmentVariable,
        string name,
        out double parsed)
    {
        parsed = 0;
        var raw = readEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
            double.IsFinite(parsed);
    }

    private static bool TryReadDoubleAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out double parsed,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                double.IsFinite(parsed))
            {
                return true;
            }
        }

        parsed = 0;
        return false;
    }

    private static double ApplyConfiguredMaximumServerExperienceRate(
        IReadOnlyDictionary<string, string> attributes,
        Func<string, string?> readEnvironmentVariable,
        double rate)
    {
        var maximumRate = ReadMaximumServerExperienceRate(attributes, readEnvironmentVariable);
        return rate <= maximumRate ? rate : maximumRate;
    }

    private static double ReadMaximumServerExperienceRate(
        IReadOnlyDictionary<string, string> attributes,
        Func<string, string?> readEnvironmentVariable)
    {
        if (TryReadDoubleAttribute(
                attributes,
                out var maximumRate,
                MaximumMonsterKillExperienceRateAttributeName,
                MaximumServerExperienceRateAttributeName) &&
            maximumRate >= 0)
        {
            return maximumRate;
        }

        return TryReadEnvironmentRate(
                readEnvironmentVariable,
                MaximumMonsterExperienceRateEnvironmentVariableName,
                out maximumRate) &&
            maximumRate >= 0
                ? maximumRate
                : DefaultMaximumServerExperienceRate;
    }

    private static void ValidateInputs(LegacyExperienceGainInputs inputs)
    {
        if (!double.IsFinite(inputs.BaseAmount) || inputs.BaseAmount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputs),
                inputs.BaseAmount,
                "Legacy experience amount cannot be negative.");
        }

        if (!double.IsFinite(inputs.ServerExperienceRate) || inputs.ServerExperienceRate < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputs),
                inputs.ServerExperienceRate,
                "Legacy server experience rate cannot be negative.");
        }

        if (!double.IsFinite(inputs.DoubleExperienceRate) || inputs.DoubleExperienceRate < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputs),
                inputs.DoubleExperienceRate,
                "Legacy double experience rate cannot be negative.");
        }
    }
}
