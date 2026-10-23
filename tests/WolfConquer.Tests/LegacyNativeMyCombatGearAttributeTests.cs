using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeMyCombatGearAttributeTests
{
    [Theory]
    [InlineData(1u, "MaxHitpoints", 259.0)] [InlineData(2u, "MinAttack", 259.0)]
    [InlineData(3u, "Defence", 259.0)] [InlineData(4u, "MagicAttack", 259.0)]
    [InlineData(5u, "MagicDefence", 259.0)] [InlineData(6u, "PhysicalDamageIncrease", 259.0)]
    [InlineData(7u, "PhysicalDamageDecrease", 259.0)] [InlineData(8u, "MagicDamageIncrease", 259.0)]
    [InlineData(9u, "MagicDamageDecrease", 259.0)] [InlineData(10u, "CriticalStrike", 259.0)]
    [InlineData(11u, "SkillCStrike", 259.0)] [InlineData(12u, "Immunity", 259.0)]
    [InlineData(13u, "Breakthrough", 25.0)] [InlineData(14u, "Counteraction", 25.0)]
    [InlineData(15u, "StrikeLock", 2.0)] [InlineData(16u, "LuckyStrike", 2.0)]
    [InlineData(17u, "CoreStrike", 2.0)] [InlineData(18u, "InvisibleArrow", 2.0)]
    [InlineData(19u, "DrainingTouch", 2.0)] [InlineData(20u, "BloodSpawn", 2.0)]
    [InlineData(21u, "KillingFlash", 2.0)] [InlineData(22u, "MirrorOfSin", 2.0)]
    [InlineData(23u, "MaxMana", 259.0)]
    public void EveryActualAttributeDispatchesToOnlyItsNativePhysicalOwners(uint type, string target, double expected)
    {
        var owner = new LegacyNativeGameClientRuntime();
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.A((LegacyNativeDaoqiDictTypeTable.AttrType)type, 259.9);
        foreach (var field in typeof(LegacyNativeMsgStatus).GetFields().Where(field => field.FieldType == typeof(uint)))
        {
            var value = (uint)field.GetValue(owner.Status)!;
            Assert.Equal(field.Name == target || (type == 2 && field.Name == "MaxAttack") ? (uint)expected : 0u, value);
        }
        foreach (var field in typeof(LegacyNativePerfectionEffect).GetFields())
            Assert.Equal(field.Name == target ? expected : 0d, (double)field.GetValue(owner.PerfectionStatus)!);
    }

    [Theory]
    [InlineData(0u)] [InlineData(24u)] [InlineData(uint.MaxValue)]
    public void UnknownAndSpellAttributesDoNotDereferenceAnAbsentUser(uint type)
    {
        new LegacyNativeMyCombatGear(null!).A((LegacyNativeDaoqiDictTypeTable.AttrType)type, double.NaN);
    }

    [Theory]
    [InlineData(double.NaN, 0u, -21474836.0)]
    [InlineData(double.PositiveInfinity, 0u, -21474836.0)]
    [InlineData(double.NegativeInfinity, 0u, -21474836.0)]
    [InlineData(-1.5, 4294967295u, 0.0)]
    [InlineData(0.59, 0u, 0.0)]
    [InlineData(100.1, 100u, 1.0)]
    [InlineData(2147483647.0, 2147483647u, 21474836.0)]
    [InlineData(2147483648.0, 2147483648u, -21474836.0)]
    [InlineData(-2147483649.0, 2147483647u, -21474836.0)]
    [InlineData(4294967295.0, 4294967295u, -21474836.0)]
    [InlineData(9223372036854775808.0, 0u, -21474836.0)]
    public void ScopedSourceCastsAndIntegerPercentDivisionRetainObservedFrameworkResults(double value, uint raw, double percent)
    {
        var owner = new LegacyNativeGameClientRuntime();
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, value);
        gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.Break, value);
        gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.Parry, value);
        Assert.Equal(raw, owner.Status.MaxHitpoints);
        Assert.Equal(raw / 10, owner.Status.Breakthrough);
        Assert.Equal(percent, owner.PerfectionStatus.StrikeLock);
    }

    [Fact]
    public void ExistingUnsignedAndDoubleOwnersAreAddedToRatherThanReset()
    {
        var owner = new LegacyNativeGameClientRuntime();
        owner.Status.MaxHitpoints = uint.MaxValue;
        owner.PerfectionStatus.LuckyStrike = 0.75;
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, 2.99);
        gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.LuckyStrike, 299.99);
        Assert.Equal(1u, owner.Status.MaxHitpoints);
        Assert.Equal(2.75, owner.PerfectionStatus.LuckyStrike);
    }
}
