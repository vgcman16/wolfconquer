using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using ExpType = WolfConquer.Core.World.LegacyNativeCombatGear.TypeExp;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearCalculationTests
{
    [Fact]
    public void UnloadedNumericMethodsReturnZeroWithoutApplyingTheirNormalCalculations()
    {
        using var load = new NativeCombatGearLookupLoadScope(false);
        Assert.Equal(0u, Gear.GetExp(ExpType.item, 730008, uint.MaxValue));
        Assert.Equal(0u, Gear.GetExp(ExpType.Plus, 8));
        Assert.Equal(0u, Gear.GetExp(ExpType.Super, 9));
        Assert.Equal(0, Gear.GetDamage(int.MaxValue, 30100, 30100, 30100, 30100, 30100, true));
        Assert.False(GearTable.m_Load);
    }

    [Fact]
    public void EveryNativeItemCoefficientUsesTheCallerSizeIncludingZero()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        (uint Id, uint Coefficient)[] cases =
        [
            (730001, 40), (730002, 160), (730003, 400), (730004, 1440),
            (730005, 4320), (730006, 12960), (730007, 38880), (730008, 116640),
            (3333400, 120), (3333401, 480), (3333402, 1440), (3333403, 4320),
            (3335125, 120), (3335126, 480), (3335127, 1440), (3335128, 4320)
        ];
        foreach (var row in cases)
        {
            Assert.Equal(0u, Gear.GetExp(ExpType.item, row.Id));
            Assert.Equal(row.Coefficient, Gear.GetExp(ExpType.item, row.Id, 1));
            Assert.Equal(row.Coefficient * 3, Gear.GetExp(ExpType.item, row.Id, 3));
        }
    }

    [Fact]
    public void AllEightPlusLevelsIgnoreSizeRatherThanMultiplyingTheirCoefficients()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        uint[] coefficients = [40, 160, 400, 1440, 4320, 12960, 38880, 116640];
        for (var i = 0; i < coefficients.Length; i++)
        {
            Assert.Equal(coefficients[i], Gear.GetExp(ExpType.Plus, (uint)i + 1, 0));
            Assert.Equal(coefficients[i], Gear.GetExp(ExpType.Plus, (uint)i + 1, uint.MaxValue));
        }
        Assert.Equal(0u, Gear.GetExp(ExpType.Plus, 0, 8));
        Assert.Equal(0u, Gear.GetExp(ExpType.Plus, 9, 8));
        Assert.Equal(0u, Gear.GetExp(ExpType.Plus, uint.MaxValue, 8));
    }

    [Fact]
    public void SuperUsesOnlyTheFinalDecimalDigitAndIgnoresSize()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        uint[] expected = [0, 0, 0, 0, 0, 0, 1, 2, 6, 20];
        for (uint digit = 0; digit < 10; digit++)
        {
            Assert.Equal(expected[digit], Gear.GetExp(ExpType.Super, 123450 + digit, 0));
            Assert.Equal(expected[digit], Gear.GetExp(ExpType.Super, digit, uint.MaxValue));
        }
        Assert.Equal(0u, Gear.GetExp(ExpType.Super, uint.MaxValue, uint.MaxValue));
    }

    [Fact]
    public void UnknownItemsAndTypeValuesDoNotAcquireAdjacentMappings()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        foreach (uint id in new uint[] { 0, 730000, 730009, 3333399, 3333404, 3335124, 3335129, uint.MaxValue })
            Assert.Equal(0u, Gear.GetExp(ExpType.item, id, uint.MaxValue));
        Assert.Equal(0u, Gear.GetExp((ExpType)3u, 730001, 100));
        Assert.Equal(0u, Gear.GetExp((ExpType)uint.MaxValue, 730001, 100));
    }

    [Theory]
    [InlineData(730001u, uint.MaxValue, 4294967256u)]
    [InlineData(730008u, uint.MaxValue, 4294850656u)]
    [InlineData(3333400u, 2147483648u, 0u)]
    [InlineData(730005u, 1000000u, 25032704u)]
    public void ItemExperienceRetainsUncheckedUInt32Multiplication(uint id, uint size, uint expected)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(expected, Gear.GetExp(ExpType.item, id, size));
    }

    [Theory]
    [InlineData(true, false, 2000)]
    [InlineData(false, false, 6000)]
    [InlineData(true, true, 1000)]
    [InlineData(false, true, 5000)]
    public void PlayerSelectsMinimumIncludingZeroWhileNonPlayerSelectsMaximum(bool player, bool missingLast, int expected)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(expected, Gear.GetDamage(1000, 30100, 30200, 30300, 30400, missingLast ? 0 : 30500, player));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EachLaterModifierUsesD1UpperBoundRatherThanItsOwn(int slot)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        var values = new int[5];
        values[slot - 1] = 31499;
        Assert.Equal(5990, Gear.GetDamage(1000, values[0], values[1], values[2], values[3], values[4], false));
        Assert.Equal(1000, Gear.GetDamage(1000, values[0], values[1], values[2], values[3], values[4], true));
    }

    [Theory]
    [InlineData(30000, 1000)]
    [InlineData(30001, 1010)]
    [InlineData(30999, 10990)]
    [InlineData(31000, 1000)]
    public void FirstModifierKeepsItsOwnStrictLowerInclusiveUpperAndModuloZeroBoundary(int first, int expected)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(expected, Gear.GetDamage(1000, first, 0, 0, 0, 0, false));
    }

    [Fact]
    public void FirstModifierAbove31000SuppressesAllLaterOtherwiseEligibleModifiers()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(1000, Gear.GetDamage(1000, 31001, 30500, int.MaxValue, 30999, 30050, false));
        Assert.Equal(1000, Gear.GetDamage(1000, 31001, 30500, int.MaxValue, 30999, 30050, true));
    }

    [Theory]
    [InlineData(30000, 1000)]
    [InlineData(30001, 1010)]
    [InlineData(31000, 1000)]
    [InlineData(31001, 1010)]
    public void LaterModifiersKeepStrictLowerBoundButBorrowTheInclusiveD1UpperBound(int modifier, int expected)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        // d1=31000 has a zero remainder yet still admits d2..d5 above 31000.
        for (var slot = 1; slot < 5; slot++)
        {
            var values = new int[5];
            values[0] = 31000;
            values[slot] = modifier;
            Assert.Equal(expected, Gear.GetDamage(1000,
                values[0], values[1], values[2], values[3], values[4], false));
        }
    }

    [Fact]
    public void NegativeFirstModifierStillPermitsTheLaterUnboundedModuloBranch()
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(7470, Gear.GetDamage(1000, int.MinValue, int.MaxValue, 0, 0, 0, false));
        Assert.Equal(1000, Gear.GetDamage(1000, int.MinValue, int.MaxValue, 0, 0, 0, true));
    }

    [Theory]
    [InlineData(int.MaxValue, 30100, 2147483646)]
    [InlineData(int.MinValue, 30100, int.MinValue)]
    [InlineData(int.MaxValue, 30001, -2126008813)]
    [InlineData(-101, 30001, -102)]
    [InlineData(-101, 30999, -1109)]
    public void DamageKeepsSignedMultiplicationAdditionOverflowAndTruncatingDivision(int damage, int first, int expected)
    {
        using var load = new NativeCombatGearLookupLoadScope();
        Assert.Equal(expected, Gear.GetDamage(damage, first, 0, 0, 0, 0, false));
    }
}

internal sealed class NativeCombatGearLookupLoadScope : IDisposable
{
    private readonly bool prior = GearTable.m_Load;
    internal NativeCombatGearLookupLoadScope(bool loaded = true) => GearTable.m_Load = loaded;
    public void Dispose() => GearTable.m_Load = prior;
}
