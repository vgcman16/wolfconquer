using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeAttackBaseRateTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void IntegerRatesAlwaysConsumeExactlyOneActualUnboundedDraw(int value)
    {
        using var random = new NativeAttackRandomScope(7319);
        var raw = random.Control.Next();

        Assert.Equal(value > raw % 100, LegacyNativeAttackBase.Rate(value));

        random.AssertContinuation();
    }

    [Theory]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MinValue)]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(double.Epsilon)]
    [InlineData(0.5)]
    [InlineData(50.5)]
    [InlineData(99.5)]
    [InlineData(100.0)]
    [InlineData(101.0)]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void DoubleRatesKeepNativeComparisonAndStillDrawForNaNAndInfinities(double value)
    {
        using var random = new NativeAttackRandomScope(8231);
        var raw = random.Control.Next();

        Assert.Equal(value > (double)raw % 100.0, LegacyNativeAttackBase.Rate(value));

        random.AssertContinuation();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualityFailsWhileTheNextIntegerOrFractionalThresholdPasses(bool fractional)
    {
        using var random = new NativeAttackRandomScope(6029);
        var firstRoll = random.Control.Next() % 100;
        Assert.False(fractional
            ? LegacyNativeAttackBase.Rate((double)firstRoll)
            : LegacyNativeAttackBase.Rate(firstRoll));
        var secondRoll = random.Control.Next() % 100;
        Assert.True(fractional
            ? LegacyNativeAttackBase.Rate(secondRoll + 0.5)
            : LegacyNativeAttackBase.Rate(secondRoll + 1));
        random.AssertContinuation();
    }

    [Fact]
    public void CalculateOwnerDoesNotConsumeOrReplaceTheDistinctPoolRandom()
    {
        var priorPool = LegacyNativePoolRandom.GetRandom;
        var pool = new LegacyNativeSafeRandom(2917);
        try
        {
            LegacyNativePoolRandom.GetRandom = pool;
            using var random = new NativeAttackRandomScope(6197);
            Assert.NotSame(pool, LegacyNativeAttackBase.MyRandom);
            Assert.Equal(60 > random.Control.Next() % 100, LegacyNativeAttackBase.Rate(60));
            Assert.Equal(30.5 > (double)random.Control.Next() % 100.0, LegacyNativeAttackBase.Rate(30.5));
            Assert.Same(pool, LegacyNativePoolRandom.GetRandom);
            Assert.Equal(new Random(2917).Next(), pool.Next());
            random.AssertContinuation();
        }
        finally { LegacyNativePoolRandom.GetRandom = priorPool; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpossibleRatesDoNotGuardAReplacedNullRandom(bool fractional)
    {
        using var random = new NativeAttackRandomScope(7127);
        LegacyNativeAttackBase.MyRandom = null!;
        Assert.Throws<NullReferenceException>(() =>
        {
            if (fractional) LegacyNativeAttackBase.Rate(double.NaN);
            else LegacyNativeAttackBase.Rate(int.MinValue);
        });
        Assert.Null(LegacyNativeAttackBase.MyRandom);
    }
}

// Test-only MODEL of a replaceable native static owner, not a production RNG callback.
internal sealed class NativeAttackRandomScope : IDisposable
{
    private readonly LegacyNativeSafeRandom prior = LegacyNativeAttackBase.MyRandom;
    internal Random Control { get; }

    internal NativeAttackRandomScope(int seed)
    {
        Control = new Random(seed);
        LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(seed);
    }

    internal void AssertDrawCount(int count)
    {
        for (var i = 0; i < count; i++) Control.Next();
        AssertContinuation();
    }

    internal void AssertContinuation() => Assert.Equal(Control.Next(), LegacyNativeAttackBase.MyRandom.Next());

    internal static int FindSeed(int draws, Func<int[], bool> accepts)
    {
        var values = new int[draws];
        for (var seed = 1; seed <= 1_000_000; seed++)
        {
            var candidate = new Random(seed);
            for (var i = 0; i < values.Length; i++) values[i] = candidate.Next() % 100;
            if (accepts(values)) return seed;
        }
        throw new InvalidOperationException("No real seeded Random met the bounded gem fixture conditions.");
    }

    public void Dispose() => LegacyNativeAttackBase.MyRandom = prior;
}
