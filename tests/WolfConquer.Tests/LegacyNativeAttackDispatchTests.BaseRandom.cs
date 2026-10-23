using WolfConquer.Core.World;
using Base = WolfConquer.Core.World.LegacyNativeAttackBase;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public Task BaseRefineryUsesRealSharedRngOnlyAfterSignedWrappedDifferenceAdmission() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Base.MyRandom = new(29); var expected = new LegacyNativeSafeRandom(29);
        Assert.False(Base.GetRefinery(1, 1)); Assert.False(Base.GetRefinery(0, uint.MaxValue));
        Assert.Equal(10 > expected.Next() % 100, Base.GetRefinery(20, 10));
        Assert.False(Base.GetRefinery(uint.MaxValue, 0)); expected.Next(); //Wrapped -1 still consumes one genuine draw.
        Assert.Equal(40 > expected.Next() % 100, Base.GetRefinery());
        Assert.Equal(expected.Next(), Base.MyRandom.Next());
    });

    [Theory]
    [InlineData(100u, 50u, 50)] [InlineData(50u, 50u, 1)] [InlineData(40u, 50u, 1)]
    public Task BaseDamageUsesNativeHalfRandomAndRepairsReversedBoundsWithoutSwapping(uint maximum, uint minimum, int bound) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Base.MyRandom = new(31); var expected = new LegacyNativeSafeRandom(31);
        Assert.Equal(minimum + (uint)(expected.Next(bound) / 2), Base.GetDamage(maximum, minimum));
        Assert.Equal(expected.Next(), Base.MyRandom.Next());
        Assert.Equal(30u, Base.GetFinalDmg(100, 10, 30)); //Native passes (min,max) into (MaxAttack,MinAttack).
        expected.Next(1); Assert.Equal(expected.Next(), Base.MyRandom.Next());
        Assert.Equal(20u, Base.GetFinalDmg(20, 10, 30));
        Assert.Equal(expected.Next(), Base.MyRandom.Next()); //Below maximum consumes no draw.
    });

    [Fact]
    public Task BaseDamagePreservesSignedRangeNarrowingFaultRatherThanUsingAUIntWideRandom() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Base.MyRandom = new(31);
        Assert.Throws<ArgumentOutOfRangeException>(() => Base.GetDamage(2147483648u, 0));
        Assert.Equal(uint.MaxValue, Base.GetDamage(uint.MaxValue, uint.MaxValue));
    });
}
