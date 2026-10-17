using WolfConquer.Core.World;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterMagicRetaliationTests
{
    [Theory]
    [InlineData(100, 0, MsgAttackEffect.TailedBeast)]
    [InlineData(100, 19, MsgAttackEffect.TailedBeast)]
    [InlineData(100, 20, MsgAttackEffect.None)]
    [InlineData(4, 0, MsgAttackEffect.None)]
    public void MagicRetaliationTailedBeastEffectUsesSourceLevelDividedByFiveRate(
        uint level,
        uint roll,
        MsgAttackEffect expected)
    {
        var attributes = new Dictionary<string, string>
        {
            ["TailedBeastActivated"] = "True",
            ["TailedBeastLevel"] = level.ToString(),
        };

        var effect = LegacyMonsterMagicRetaliation.ResolveTailedBeastEffect(attributes, roll: roll);

        Assert.Equal(expected, effect);
    }

    [Fact]
    public void MagicRetaliationTailedBeastEffectRequiresActivatedBeast()
    {
        var attributes = new Dictionary<string, string>
        {
            ["TailedBeastActivated"] = "False",
            ["TailedBeastLevel"] = "100",
        };

        var effect = LegacyMonsterMagicRetaliation.ResolveTailedBeastEffect(attributes, roll: 0);

        Assert.Equal(MsgAttackEffect.None, effect);
    }

    [Fact]
    public void MagicRetaliationTailedBeastEffectReadsLegacyBeastsAliases()
    {
        var attributes = new Dictionary<string, string>
        {
            ["BeastsActivated"] = "1",
            ["BeastsLevel"] = "100",
        };

        var effect = LegacyMonsterMagicRetaliation.ResolveTailedBeastEffect(attributes, roll: 19);

        Assert.Equal(MsgAttackEffect.TailedBeast, effect);
    }

    [Fact]
    public void MagicRetaliationTailedBeastEffectCanResolveLevelFromTotalPoints()
    {
        var attributes = new Dictionary<string, string>
        {
            ["TailedBeastActivated"] = "1",
            ["TailedBeastTotalPoints"] = "10",
        };
        var beastAttributes = new Dictionary<int, BeastAttribute>
        {
            [1] = BeastAttribute(1, requiredPoints: 1),
            [2] = BeastAttribute(2, requiredPoints: 1),
            [3] = BeastAttribute(3, requiredPoints: 1),
            [4] = BeastAttribute(4, requiredPoints: 1),
            [5] = BeastAttribute(5, requiredPoints: 1),
        };

        var effect = LegacyMonsterMagicRetaliation.ResolveTailedBeastEffect(attributes, beastAttributes, roll: 0);

        Assert.Equal(MsgAttackEffect.TailedBeast, effect);
    }

    private static BeastAttribute BeastAttribute(int level, int requiredPoints)
    {
        return new BeastAttribute(
            level,
            [requiredPoints],
            "beasts_attr.txt",
            level,
            string.Empty,
            []);
    }
}
