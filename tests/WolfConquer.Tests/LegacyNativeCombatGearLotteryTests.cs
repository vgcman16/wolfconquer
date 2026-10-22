using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Flag = WolfConquer.Core.World.LegacyNativeCombatGear.LotteryEffect;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearLotteryTests
{
    [Theory]
    [InlineData(0u, 0UL, false, 0u, 0u)]
    [InlineData(1u, 3UL, true, 1u, 0u)]
    [InlineData(0u, 3UL, false, 3u, 0u)]
    [InlineData(3u, 1UL, true, 3u, 2u)]
    [InlineData(0x80000000u, 0x80000001UL, true, 0x80000000u, 0u)]
    [InlineData(1u, 0x100000000UL, false, 1u, 1u)]
    [InlineData(0u, 0x180000001UL, false, 0x80000001u, 0u)]
    [InlineData(uint.MaxValue, ulong.MaxValue, true, uint.MaxValue, 0u)]
    public void FlagMasksTruncateToLow32AndCompositeContainmentMeansAnyOverlap(
        uint initial, ulong mask, bool contained, uint afterAdd, uint afterRemove)
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Gear.Lottery = initial;
        var flag = (Flag)mask;

        Assert.Equal(contained, f.Gear.ContainsFlag(flag));
        var added = f.Gear.AddFlag(flag);

        Assert.Equal(afterAdd, f.Gear.Lottery);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: afterAdd), added);
        f.Gear.Lottery = initial;
        var removed = f.Gear.RemoveFlag(flag);
        Assert.Equal(afterRemove, f.Gear.Lottery);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: afterRemove), removed);
        f.AssertContinuation(); // Flag methods do not draw from the actual RNG.
    }

    [Fact]
    public void RepeatedAddsAndAbsentRemovalsStillProduceFreshRequiredLotteryFrames()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Gear.TaoistReiki = f.Gear.MasteryPoints = f.Gear.PirateExp = uint.MaxValue;

        var first = f.Gear.AddFlag(Flag.Free10Draws);
        var duplicate = f.Gear.AddFlag(Flag.Free10Draws);
        var removed = f.Gear.RemoveFlag(Flag.Free10Draws);
        var absent = f.Gear.RemoveFlag(Flag.Free10Draws);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: 2), first);
        Assert.Equal(first, duplicate);
        Assert.NotSame(first, duplicate);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(12), removed);
        Assert.Equal(removed, absent);
        Assert.NotSame(removed, absent);
        Assert.Equal(0u, f.Gear.Lottery);
        Assert.Equal(uint.MaxValue, f.Gear.TaoistReiki);
        Assert.Equal(uint.MaxValue, f.Gear.MasteryPoints);
        Assert.Equal(uint.MaxValue, f.Gear.PirateExp);
    }

    [Fact]
    public void UnloadedWireGateDoesNotGuardLotteryMutationsOrContainment()
    {
        using var f = new NativeCombatGearSupportFixture(false);
        Gear.GetRandom = null!;

        Assert.Null(f.Gear.AddFlag(Flag.Free10Draws));
        Assert.Equal(2u, f.Gear.Lottery);
        Assert.True(f.Gear.ContainsFlag(Flag.Free10Draws));
        Assert.Null(f.Gear.AddFlag((Flag)3UL)); // Existing bit2 blocks adding composite's bit1.
        Assert.Equal(2u, f.Gear.Lottery);
        Assert.Null(f.Gear.RemoveFlag(Flag.Free10Draws));
        Assert.Equal(0u, f.Gear.Lottery);
        Assert.False(f.Gear.ContainsFlag(Flag.Free10Draws));
        Assert.Null(Gear.GetRandom);
        Assert.False(GearTable.m_Load);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReikiUpdateUsesTheActualLoadGateAndOnlyItsOwnScalarWithoutMutatingState(bool loaded)
    {
        using var f = new NativeCombatGearSupportFixture(loaded);
        f.Gear.TaoistReiki = uint.MaxValue;
        f.Gear.Lottery = f.Gear.MasteryPoints = f.Gear.PirateExp = 99;
        Gear.GetRandom = null!;

        var frame = f.Gear.UpdateTaoistReiki();

        if (loaded) Assert.Equal(NativeCombatGearSupportFixture.Frame(3, reiki: uint.MaxValue), frame);
        else Assert.Null(frame);
        Assert.Equal(uint.MaxValue, f.Gear.TaoistReiki);
        Assert.Equal(99u, f.Gear.Lottery);
        Assert.Equal(99u, f.Gear.MasteryPoints);
        Assert.Equal(99u, f.Gear.PirateExp);
        Assert.Null(Gear.GetRandom);
    }

    [Theory]
    [InlineData(int.MinValue, 100)]
    [InlineData(-1, 100)]
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(17, -100)]
    [InlineData(17, int.MinValue)]
    [InlineData(0, -1)]
    public void RateConsumesOneActualUnboundedDrawAndPreservesTheSignedRemainderContract(int value, int discriminant)
    {
        using var f = new NativeCombatGearSupportFixture(seed: 8123);
        var raw = f.Control.Next();

        Assert.Equal(value > raw % discriminant, Gear.Rate(value, discriminant));

        f.AssertContinuation();
    }

    [Fact]
    public void RateUsesStrictThresholdComparison()
    {
        using var f = new NativeCombatGearSupportFixture(seed: 8161);
        var first = f.Control.Next() % 100;
        Assert.False(Gear.Rate(first, 100));
        var second = f.Control.Next() % 100;
        Assert.True(Gear.Rate(second + 1, 100));
        f.AssertContinuation();
    }

    [Fact]
    public void ZeroDiscriminantFaultStillConsumesThePriorRandomDraw()
    {
        using var f = new NativeCombatGearSupportFixture(seed: 8171);

        Assert.Throws<DivideByZeroException>(() => Gear.Rate(17, 0));

        f.Control.Next();
        f.AssertContinuation();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnloadedRateReturnsFalseBeforeNullRngOrZeroModulo(bool nullRandom)
    {
        using var f = new NativeCombatGearSupportFixture(false, 8191);
        if (nullRandom) Gear.GetRandom = null!;

        Assert.False(Gear.Rate(int.MaxValue, 0));

        if (nullRandom) Assert.Null(Gear.GetRandom);
        else f.AssertContinuation();
    }

    [Fact]
    public void LoadedNullRandomFaultPrecedesZeroModuloWithoutInstallingAReplacement()
    {
        using var f = new NativeCombatGearSupportFixture();
        Gear.GetRandom = null!;

        Assert.Throws<NullReferenceException>(() => Gear.Rate(17, 0));

        Assert.Null(Gear.GetRandom);
        Assert.True(GearTable.m_Load);
    }
}
