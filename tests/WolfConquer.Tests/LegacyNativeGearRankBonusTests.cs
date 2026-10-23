using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeGearRankBonusTests
{
    [Theory]
    [InlineData(2001u, 2d, 5d, 0d, 0u, 0u, 0u, 0u, 0, 0)]
    [InlineData(4001u, 2d, 5d, 0d, 500u, 500u, 300u, 0u, 0, 0)]
    [InlineData(13001u, 0d, 6d, 0d, 0u, 500u, 300u, 500u, 0, 0)]
    [InlineData(14001u, 0d, 0d, 9d, 0u, 500u, 300u, 500u, 0, 0)]
    [InlineData(7001u, 0d, 0d, 0d, 0u, 0u, 0u, 0u, 21930, 10)]
    public void WholeRankBonusRetainsEveryNativeClassFamily(uint job, double lucky,
        double parry, double core, uint attack, uint defence, uint magicDefence,
        uint prestige, ushort magic, ushort level)
    {
        using var scope = new NativeCombatGearQueryScope();
        LegacyNativeCombatGearRankTable.Create();
        foreach (var rank in LegacyNativeCombatGearRankTable.Ranks.Values)
            rank.RankingList[17] = new() { UID = 17 };
        var value = LegacyNativeCombatGear.GetValueRank(17, job)!;
        Assert.Equal(lucky, value.LukyStrike);
        Assert.Equal(parry, value.Parry);
        Assert.Equal(core, value.CoreStrike);
        Assert.Equal(attack, value.PAttack);
        Assert.Equal(defence, value.PDefense);
        Assert.Equal(magicDefence, value.MDefense);
        Assert.Equal(prestige, value.PrestigeScore);
        Assert.Equal(magic, value.Magic);
        Assert.Equal(level, value.MagicLevel);
        Assert.NotSame(value, LegacyNativeCombatGear.GetValueRank(17, job));
    }

    [Fact]
    public void DisabledTableReturnsNullBeforeClassOrRankLookupAndUnknownClassIsFreshZero()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        Assert.Null(LegacyNativeCombatGear.GetValueRank(17, uint.MaxValue));
        LegacyNativeCombatGearTable.m_Load = true;
        var value = LegacyNativeCombatGear.GetValueRank(17, 0)!;
        Assert.NotNull(value);
        Assert.All(typeof(LegacyNativeCombatGear.VRANK).GetFields(),
            field => Assert.Equal(0d, Convert.ToDouble(field.GetValue(value))));
        Assert.NotSame(value, LegacyNativeCombatGear.GetValueRank(17, 0));
    }
}
