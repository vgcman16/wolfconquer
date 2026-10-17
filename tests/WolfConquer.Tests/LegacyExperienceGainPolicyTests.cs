using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyExperienceGainPolicyTests
{
    [Theory]
    [InlineData(3, 100, LegacyExperienceGainSuppressionReason.Cursed)]
    [InlineData(2, 140, LegacyExperienceGainSuppressionReason.MaxLevel)]
    public void IncreaseExperienceSuppressesCursedAndMaxLevel(
        int cursedTimer,
        int playerLevel,
        LegacyExperienceGainSuppressionReason expectedReason)
    {
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(cursedTimer: cursedTimer, playerLevel: playerLevel));

        Assert.True(result.Suppressed);
        Assert.Equal(expectedReason, result.SuppressionReason);
        Assert.Equal(0UL, result.AppliedExperienceGain);
        Assert.Equal(0UL, result.OblivionExperienceGain);
        Assert.Equal(0U, result.HuntingBlessingGain);
    }

    [Fact]
    public void AttackUpdateSuppressesZeroDamageBeforeOblivionAndHeavenBlessing()
    {
        var result = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            Inputs(baseAmount: 0, hasOblivion: true, hasHeavenBlessing: true));

        Assert.True(result.Suppressed);
        Assert.Equal(LegacyExperienceGainSuppressionReason.ZeroDamage, result.SuppressionReason);
        Assert.Equal(0UL, result.AppliedExperienceGain);
        Assert.Equal(0UL, result.OblivionExperienceGain);
        Assert.Equal(0U, result.HuntingBlessingGain);
        Assert.False(result.RedirectedToOblivion);
        Assert.False(result.GrantsHuntingBlessing);
    }

    [Fact]
    public void AttackUpdateRedirectsOblivionAndStillAwardsHuntingBlessing()
    {
        var result = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            Inputs(baseAmount: 125, hasOblivion: true, hasHeavenBlessing: true));

        Assert.False(result.Suppressed);
        Assert.Equal(LegacyExperienceGainSuppressionReason.None, result.SuppressionReason);
        Assert.Equal(0UL, result.AppliedExperienceGain);
        Assert.Equal(500UL, result.OblivionExperienceGain);
        Assert.Equal(12U, result.HuntingBlessingGain);
        Assert.True(result.RedirectedToOblivion);
        Assert.True(result.GrantsHuntingBlessing);
    }

    [Fact]
    public void IncreaseExperienceAppliesSoulChantReductionBeforeOtherRates()
    {
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(
                baseAmount: 1_000,
                soulChantEquipped: true,
                soulChantItemLevel: 3));

        Assert.False(result.Suppressed);
        Assert.Equal(600UL, result.AppliedExperienceGain);
    }

    [Fact]
    public void IncreaseExperienceAppliesServerRainbowDoubleAndMap1039InSourceOrderWithTruncation()
    {
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(
                baseAmount: 1_111,
                serverExperienceRate: 2,
                rainbowGemPercent: 25,
                doubleExperienceTime: 1,
                doubleExperienceRate: 1.5,
                mapId: 1039));

        Assert.False(result.Suppressed);
        Assert.Equal(41UL, result.AppliedExperienceGain);
    }

    [Theory]
    [InlineData(LegacyExperienceGainPolicy.UserExperienceRateAttributeName)]
    [InlineData(LegacyExperienceGainPolicy.ServerExperienceRateAttributeName)]
    [InlineData(LegacyExperienceGainPolicy.ExperienceRateAttributeName)]
    [InlineData(LegacyExperienceGainPolicy.MonsterKillExperienceRateAttributeName)]
    public void ReadServerExperienceRateAppliesAttributeAliasesToFormula(string attributeName)
    {
        var attributes = new Dictionary<string, string>
        {
            [attributeName] = "2",
        };

        var rate = LegacyExperienceGainPolicy.ReadServerExperienceRate(attributes, _ => null);
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(baseAmount: 125, serverExperienceRate: rate));

        Assert.Equal(2, rate);
        Assert.False(result.Suppressed);
        Assert.Equal(250UL, result.AppliedExperienceGain);
    }

    [Theory]
    [InlineData(LegacyExperienceGainPolicy.UserExperienceRateEnvironmentVariableName)]
    [InlineData(LegacyExperienceGainPolicy.MonsterExperienceRateEnvironmentVariableName)]
    public void ReadServerExperienceRateAppliesEnvironmentAliasesToFormula(string environmentVariableName)
    {
        var rate = LegacyExperienceGainPolicy.ReadServerExperienceRate(
            new Dictionary<string, string>(),
            name => name == environmentVariableName ? "2.5" : null);
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(baseAmount: 100, serverExperienceRate: rate));

        Assert.Equal(2.5, rate);
        Assert.False(result.Suppressed);
        Assert.Equal(250UL, result.AppliedExperienceGain);
    }

    [Fact]
    public void ReadServerExperienceRateKeepsExistingNamesAheadOfMonsterAliases()
    {
        var attributes = new Dictionary<string, string>
        {
            [LegacyExperienceGainPolicy.UserExperienceRateAttributeName] = "2",
            [LegacyExperienceGainPolicy.MonsterKillExperienceRateAttributeName] = "7",
        };

        var attributeRate = LegacyExperienceGainPolicy.ReadServerExperienceRate(
            attributes,
            name => name switch
            {
                LegacyExperienceGainPolicy.UserExperienceRateEnvironmentVariableName => "3",
                LegacyExperienceGainPolicy.MonsterExperienceRateEnvironmentVariableName => "9",
                _ => null,
            });
        var environmentRate = LegacyExperienceGainPolicy.ReadServerExperienceRate(
            new Dictionary<string, string>(),
            name => name switch
            {
                LegacyExperienceGainPolicy.UserExperienceRateEnvironmentVariableName => "3",
                LegacyExperienceGainPolicy.MonsterExperienceRateEnvironmentVariableName => "9",
                _ => null,
            });

        Assert.Equal(2, attributeRate);
        Assert.Equal(3, environmentRate);
    }

    [Fact]
    public void ReadServerExperienceRateCapsSourceStyleRatesByDefault()
    {
        var attributes = new Dictionary<string, string>
        {
            [LegacyExperienceGainPolicy.UserExperienceRateAttributeName] = "1000",
        };

        var rate = LegacyExperienceGainPolicy.ReadServerExperienceRate(attributes, _ => null);
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(baseAmount: 125, serverExperienceRate: rate));

        Assert.Equal(LegacyExperienceGainPolicy.DefaultMaximumServerExperienceRate, rate);
        Assert.False(result.Suppressed);
        Assert.Equal(1_250UL, result.AppliedExperienceGain);
    }

    [Fact]
    public void ReadServerExperienceRateAllowsConfiguredMaximumOverride()
    {
        var attributes = new Dictionary<string, string>
        {
            [LegacyExperienceGainPolicy.MonsterKillExperienceRateAttributeName] = "25",
            [LegacyExperienceGainPolicy.MaximumMonsterKillExperienceRateAttributeName] = "30",
        };

        var rate = LegacyExperienceGainPolicy.ReadServerExperienceRate(attributes, _ => null);
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(baseAmount: 100, serverExperienceRate: rate));

        Assert.Equal(25, rate);
        Assert.False(result.Suppressed);
        Assert.Equal(2_500UL, result.AppliedExperienceGain);
    }

    [Fact]
    public void ReadServerExperienceRateUsesMaximumEnvironmentOverride()
    {
        var rate = LegacyExperienceGainPolicy.ReadServerExperienceRate(
            new Dictionary<string, string>(),
            name => name switch
            {
                LegacyExperienceGainPolicy.UserExperienceRateEnvironmentVariableName => "25",
                LegacyExperienceGainPolicy.MaximumMonsterExperienceRateEnvironmentVariableName => "5",
                _ => null,
            });
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(baseAmount: 100, serverExperienceRate: rate));

        Assert.Equal(5, rate);
        Assert.False(result.Suppressed);
        Assert.Equal(500UL, result.AppliedExperienceGain);
    }

    [Fact]
    public void IncreaseExperienceSuppressesWhenSoulChantReducesAmountToZero()
    {
        var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            Inputs(
                baseAmount: 1_000,
                soulChantEquipped: true,
                soulChantItemLevel: 9));

        Assert.True(result.Suppressed);
        Assert.Equal(LegacyExperienceGainSuppressionReason.SoulChantReducedToZero, result.SuppressionReason);
        Assert.Equal(0UL, result.AppliedExperienceGain);
    }

    private static LegacyExperienceGainInputs Inputs(
        double baseAmount = 1_000,
        int playerLevel = 100,
        int cursedTimer = 0,
        bool soulChantEquipped = false,
        byte soulChantItemLevel = 0,
        double serverExperienceRate = 1,
        int rainbowGemPercent = 0,
        int doubleExperienceTime = 0,
        double doubleExperienceRate = 1,
        int mapId = 1002,
        bool hasOblivion = false,
        bool hasHeavenBlessing = false)
    {
        return new LegacyExperienceGainInputs(
            baseAmount,
            playerLevel,
            cursedTimer,
            soulChantEquipped,
            soulChantItemLevel,
            serverExperienceRate,
            rainbowGemPercent,
            doubleExperienceTime,
            doubleExperienceRate,
            mapId,
            hasOblivion,
            hasHeavenBlessing);
    }
}
