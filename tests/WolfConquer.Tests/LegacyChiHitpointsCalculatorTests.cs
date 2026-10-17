using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyChiHitpointsCalculatorTests
{
    [Fact]
    public void CalculateSumsRolledHpAddAttributesFromUnlockedPowers()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            Powers:
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Phoenix,
                    "1/0/6/1200/0/7/2000/0/6/1300/0/2/200/0/"),
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Tiger,
                    "1/0/8/2500/0/3/200/0/6/1400/0/2/200/0/"),
            ]);

        var contribution = LegacyChiHitpointsCalculator.Calculate(state);

        Assert.Equal(3_900U, contribution.RolledHitpoints);
        Assert.Equal(0U, contribution.FateHitpoints);
        Assert.Equal(3_900U, contribution.Total);
        Assert.Equal(["Chi.FateExperience"], contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAddsSourceBackedFateHpAddWhenCatalogRowsAreAvailable()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            Powers:
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Phoenix,
                    "1/5000/6/1200/0/7/2000/0/8/2500/0/2/200/0/"),
            ]);
        var fateExperienceByType = new Dictionary<int, IReadOnlyList<FateExperience>>
        {
            [(int)LegacyChiPowerType.Phoenix] =
            [
                Fate(1, LegacyChiPowerType.Phoenix, level: 1, experience: 1_000, values: [60_750, 70_100, 0, 0]),
                Fate(2, LegacyChiPowerType.Phoenix, level: 2, experience: 4_000, values: [60_900, 80_100, 0, 0]),
                Fate(3, LegacyChiPowerType.Phoenix, level: 3, experience: 9_000, values: [61_100, 80_100, 0, 0]),
            ],
        };

        var contribution = LegacyChiHitpointsCalculator.Calculate(state, fateExperienceByType);

        Assert.Equal(1_200U, contribution.RolledHitpoints);
        Assert.Equal(900U, contribution.FateHitpoints);
        Assert.Equal(2_100U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateReportsDragonRankMissingWhenScoreBonusCannotBeProven()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            Powers:
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Dragon,
                    "1/0/6/1200/0/7/2000/0/8/2500/0/2/200/0/"),
            ]);

        var contribution = LegacyChiHitpointsCalculator.Calculate(
            state,
            new Dictionary<int, IReadOnlyList<FateExperience>>());

        Assert.Equal(1_200U, contribution.Total);
        Assert.Equal(["Chi.DragonRank"], contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAddsDragonPerfectScoreBonusWithoutRankInput()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            Powers:
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Dragon,
                    "1/0/6/3500/0/7/2000/0/8/2500/0/2/200/0/"),
            ]);

        var contribution = LegacyChiHitpointsCalculator.Calculate(
            state,
            new Dictionary<int, IReadOnlyList<FateExperience>>());

        Assert.Equal(3_500U, contribution.RolledHitpoints);
        Assert.Equal(5_000U, contribution.DragonScoreBonusHitpoints);
        Assert.Equal(8_500U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAppliesBoundedLegacyPercent()
    {
        var state = new LegacyChiState(
            ChiPoints: 0,
            Powers:
            [
                LegacyChiPowerState.Parse(
                    LegacyChiPowerType.Turtle,
                    "1/0/6/1501/0/7/2000/0/8/2500/0/2/200/0/"),
            ]);

        var contribution = LegacyChiHitpointsCalculator.Calculate(state, percent: 50);

        Assert.Equal(1_501U, contribution.RolledHitpoints);
        Assert.Equal(750U, contribution.Total);
    }

    private static FateExperience Fate(
        int id,
        LegacyChiPowerType type,
        int level,
        long experience,
        IReadOnlyList<long> values)
    {
        return new FateExperience(
            id,
            (int)type,
            level,
            experience,
            values,
            "fate_exp.txt",
            id,
            "",
            []);
    }
}
