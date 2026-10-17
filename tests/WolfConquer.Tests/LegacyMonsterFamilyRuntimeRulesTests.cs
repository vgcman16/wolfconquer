using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterFamilyRuntimeRulesTests
{
    [Fact]
    public void ExtraBossCopyRuleForcesAttackRangeToTwelve()
    {
        var monster = new Monster(
            9100,
            "RangeBoss",
            new Dictionary<string, string>
            {
                ["life"] = "150000",
                ["Boss"] = "1",
                ["attack_range"] = "0001",
            },
            []);

        Assert.True(LegacyMonsterFamilyRuntimeRules.UsesExtraBossCopyRules(monster));
        Assert.Equal(12U, LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monster));
    }

    [Fact]
    public void NormalMonsterUsesRawAttackRange()
    {
        var monster = new Monster(
            9101,
            "NormalMonster",
            new Dictionary<string, string>
            {
                ["life"] = "90000",
                ["Boss"] = "0",
                ["attack_range"] = "0004",
            },
            []);

        Assert.False(LegacyMonsterFamilyRuntimeRules.UsesExtraBossCopyRules(monster));
        Assert.Equal(4U, LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monster));
    }

    [Fact]
    public void BossOutsideLegacyLifeWindowUsesRawAttackRange()
    {
        var monster = new Monster(
            9102,
            "HugeBoss",
            new Dictionary<string, string>
            {
                ["life"] = "7000000",
                ["Boss"] = "1",
                ["attack_range"] = "0008",
            },
            []);

        Assert.False(LegacyMonsterFamilyRuntimeRules.UsesExtraBossCopyRules(monster));
        Assert.Equal(8U, LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monster));
    }

    [Theory]
    [InlineData(20_211, "GoldBox")]
    [InlineData(40_121, "DarkCrystalofSky")]
    [InlineData(40_122, "DarkCrystalofWind")]
    [InlineData(40_123, "ThunderCrystal")]
    [InlineData(40_124, "DarkCrystalofFire")]
    public void SourceExcludedFamiliesDoNotUseGenericAggro(int familyId, string name)
    {
        var monster = CreateMonster(familyId, name);

        Assert.True(LegacyMonsterFamilyRuntimeRules.IsExcludedFromGenericAggro(monster));
    }

    [Fact]
    public void OrdinaryMonsterCanUseGenericAggro()
    {
        var monster = CreateMonster(10, "Pheasant");

        Assert.False(LegacyMonsterFamilyRuntimeRules.IsExcludedFromGenericAggro(monster));
    }

    private static Monster CreateMonster(int familyId, string name)
    {
        return new Monster(
            familyId,
            name,
            new Dictionary<string, string>
            {
                ["life"] = "100",
                ["Boss"] = "0",
                ["attack_range"] = "0015",
            },
            []);
    }
}
