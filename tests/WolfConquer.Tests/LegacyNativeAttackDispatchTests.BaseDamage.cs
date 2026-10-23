using Base = WolfConquer.Core.World.LegacyNativeAttackBase;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(0u, 0, 1u)] [InlineData(100u, 0, 10u)] [InlineData(100u, 3, 40u)]
    [InlineData(100u, 4, 50u)] [InlineData(100u, 255, 50u)] [InlineData(4294967295u, 255, 2147483647u)]
    public void BasePoisonUsesNativeLongPromotionAndMinimumOne(uint hp, byte level, uint expected) => Assert.Equal(expected, Base.CalculatePoisonDamage(hp, level));

    [Theory]
    [InlineData(100u, 200u, 50u, 100u)] [InlineData(100u, 200u, 150u, 50u)]
    [InlineData(100u, 200u, 200u, 0u)] [InlineData(100u, 200u, 201u, 0u)]
    public void BaseHealthDamageRetainsExactRoomAndOverMaximumBehavior(uint damage, uint maximum, uint current, uint expected) =>
        Assert.Equal(expected, Base.CalculateHealtDmg(damage, maximum, current));

    [Fact]
    public void BaseDamageModifiersKeepSourceWrappingFlooringAndEmptySoulModifier()
    {
        Assert.Equal(50u, Base.CalculateBless(100, 50)); Assert.Equal(1u, Base.CalculateBless(100, 100)); Assert.Equal(1u, Base.CalculateBless(100, 200));
        Assert.Equal(50u, Base.CalcaulateDeffence(100, 50)); Assert.Equal(1u, Base.CalcaulateDeffence(100, 100));
        Assert.Equal(130u, Base.CalculateExtraAttack(100, 50, 20)); Assert.Equal(1u, Base.CalculateExtraAttack(100, 50, 200));
        Assert.Equal(1u, Base.CalculateExtraAttack(uint.MaxValue, 1, 0));
        Assert.Equal(123u, Base.CalculateSoul(123, 0)); Assert.Equal(123u, Base.CalculateSoul(123, byte.MaxValue));
        Assert.Equal(2000u, Base.CalculateArtefactsDmg(1000, 20000, 10000));
        Assert.Equal(1000u, Base.CalculateArtefactsDmg(1000, 10000, 20000)); Assert.Equal(1000u, Base.CalculateArtefactsDmg(1000, 10000, 10000));
        Assert.Equal(130, Base.CalculatePotencyDamage(100, 100, 90)); Assert.Equal(160, Base.CalculatePotencyDamage(100, 100, 90, true));
        Assert.Equal(70, Base.CalculatePotencyDamage(100, 10, 100)); Assert.Equal(100, Base.CalculatePotencyDamage(100, 100, 100));
        Assert.Equal(110, Base.CalculateSoulsDamage(100, 100, 90)); Assert.Equal(160, Base.CalculateSoulsDamage(100, 1000, 1));
        Assert.Equal(40, Base.CalculateSoulsDamage(100, 1, 1000)); Assert.Equal(100, Base.CalculateSoulsDamage(100, 100, 100));
        Assert.Equal(0u, Base.MathMin(0, uint.MaxValue)); Assert.Equal(uint.MaxValue, Base.MathMax(0, uint.MaxValue));
        Assert.Equal(9u, Base.MathMin(9, 9)); Assert.Equal(9u, Base.MathMax(9, 9)); Assert.Equal(3u, Base.MathMin(9, 3)); Assert.Equal(9u, Base.MathMax(9, 3));
    }
}
