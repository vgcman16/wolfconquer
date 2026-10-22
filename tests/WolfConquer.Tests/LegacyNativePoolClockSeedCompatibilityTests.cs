using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

// BCL/policy MODEL controls, not public game-entry, Rate4, reset-scheduler or full-stream proof.
// Oracle arithmetic is independently expressed from the pinned Microsoft reference-source Random.
public sealed class LegacyNativePoolClockSeedCompatibilityTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-int.MaxValue)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void ExplicitNonzeroSafeRandomSeedsPreserveNativeMixedOverloadStream(int seed)
    {
        var actual = new LegacyNativeSafeRandom(seed, () => throw new InvalidOperationException("Unexpected default clock read."));
        AssertMixed(actual.Next, actual.Next, actual.Next, new NativeReferenceRandom(seed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NativeDefaultClockSeedIncludingZeroIsSampledExactlyOnce(int tickCount)
    {
        var reads = 0;
        var actual = new LegacyNativeSafeRandom(0, () => { reads++; return tickCount; });
        Assert.Equal(1, reads);
        AssertMixed(actual.Next, actual.Next, actual.Next, new NativeReferenceRandom(tickCount));
        Assert.Equal(1, reads); // No first-Next/first-Rate lazy seed or repeated zero-sentinel sampling.
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    [InlineData(77)]
    [InlineData(int.MaxValue)]
    public void SetSeedIgnoresCallerArgumentAndSamplesFreshDefaultClockInsideLock(int ignoredSeed)
    {
        LegacyNativeSafeRandom? actual = null;
        var tickCount = 0;
        var reads = 0;
        actual = new LegacyNativeSafeRandom(7, () =>
        {
            Assert.NotNull(actual);
            Assert.True(Monitor.IsEntered(actual!.SyncRoot));
            reads++;
            return tickCount;
        });
        Assert.Equal(0, reads); // Nonzero constructor must not consult the native default clock.
        var ticks = new[] { int.MinValue, 0, int.MaxValue, 0 };
        for (var i = 0; i < ticks.Length; i++)
        {
            tickCount = ticks[i];
            actual.SetSeed(ignoredSeed);
            Assert.Equal(i + 1, reads);
            AssertMixed(actual.Next, actual.Next, actual.Next, new NativeReferenceRandom(tickCount));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidRangesDoNotAdvanceButDegenerateValidRangesDo(int tickCount)
    {
        var actual = new LegacyNativeSafeRandom(0, () => tickCount);
        var oracle = new NativeReferenceRandom(tickCount);
        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => actual.Next(-1));
        Assert.Equal("maxValue", negative.ParamName);
        var swapped = Assert.Throws<ArgumentOutOfRangeException>(() => actual.Next(1, 0));
        Assert.Equal("minValue", swapped.ParamName);
        Assert.Equal(oracle.Next(), actual.Next()); // Invalid calls consumed no sample.
        Assert.Equal(oracle.Next(0), actual.Next(0)); // Both consume a real sample despite returning0.
        Assert.Equal(oracle.Next(42, 42), actual.Next(42, 42));
        AssertMixed(actual.Next, actual.Next, actual.Next, oracle);
    }

    [Fact]
    public void ExplicitZeroSeedUsesLegacyPlayerAlgorithmNotNativeSafeRandomDefaultSentinel()
    {
        var actual = new Random(0); // Same explicit-seed form as Player.MyRandom if shared draw is0.
        AssertMixed(actual.Next, actual.Next, actual.Next, new NativeReferenceRandom(0));
    }

    [Fact]
    public void LocalSharedBirthDrawInterleavingAndResetDoNotReseedExistingPlayerStreams()
    {
        // Local declared component inputs only: do NOT replace process-global Pool state in tests.
        var shared = new LegacyNativeSafeRandom(123, () => 0);
        var expectedShared = new NativeReferenceRandom(123);
        var playerA = new Random(shared.Next());
        var expectedA = new NativeReferenceRandom(expectedShared.Next());
        var playerB = new Random(shared.Next());
        var expectedB = new NativeReferenceRandom(expectedShared.Next());
        for (var phase = 0; phase < 2; phase++)
        {
            for (var i = 0; i < 64; i++)
            {
                Assert.Equal(expectedA.Next(), playerA.Next());
                Assert.Equal(expectedShared.Next(17), shared.Next(17));
                Assert.Equal(expectedB.Next(int.MinValue, int.MaxValue), playerB.Next(int.MinValue, int.MaxValue));
                Assert.Equal(expectedShared.Next(-100, 101), shared.Next(-100, 101));
            }
            if (phase == 0)
            {
                shared.SetSeed(999); // Caller999 is ignored; declared DEFAULT clock0 seeds only Pool.
                expectedShared = new NativeReferenceRandom(0);
            }
        }
    }

    private static void AssertMixed(
        Func<int> next, Func<int, int> nextMax, Func<int, int, int> nextRange,
        NativeReferenceRandom oracle)
    {
        for (var i = 0; i < 128; i++)
        {
            Assert.Equal(oracle.Next(), next());
            Assert.Equal(oracle.Next(0), nextMax(0));
            Assert.Equal(oracle.Next(1), nextMax(1));
            Assert.Equal(oracle.Next(int.MaxValue), nextMax(int.MaxValue));
            Assert.Equal(oracle.Next(-1, 1), nextRange(-1, 1));
            Assert.Equal(oracle.Next(int.MinValue, int.MaxValue), nextRange(int.MinValue, int.MaxValue));
            Assert.Equal(oracle.Next(-5000, -1), nextRange(-5000, -1));
            Assert.Equal(oracle.Next(42, 42), nextRange(42, 42));
        }
    }

    // Algorithm facts from reference-source Random, not a host Random oracle or a stored outcome list.
    // No reference implementation or test has been invoked by the source-proposal author.
    private sealed class NativeReferenceRandom
    {
        private const int Limit = int.MaxValue;
        private readonly int[] _state = new int[56];
        private int _cursor;
        private int _lag = 21;

        public NativeReferenceRandom(int seed)
        {
            unchecked
            {
                var magnitude = seed == int.MinValue ? Limit : Math.Abs(seed);
                var previous = 161803398 - magnitude;
                _state[55] = previous;
                var difference = 1;
                for (var step = 1; step <= 54; step++)
                {
                    var slot = (21 * step) % 55;
                    _state[slot] = difference;
                    difference = previous - difference;
                    if (difference < 0) difference += Limit;
                    previous = _state[slot];
                }
                for (var pass = 0; pass < 4; pass++)
                    for (var slot = 1; slot <= 55; slot++)
                    {
                        _state[slot] -= _state[1 + (slot + 30) % 55];
                        if (_state[slot] < 0) _state[slot] += Limit;
                    }
            }
        }

        public int Next()
        {
            unchecked
            {
                if (++_cursor == 56) _cursor = 1;
                if (++_lag == 56) _lag = 1;
                var value = _state[_cursor] - _state[_lag];
                if (value == Limit) value--;
                if (value < 0) value += Limit;
                _state[_cursor] = value;
                return value;
            }
        }

        public int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(Next() * (1.0 / Limit) * maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            var width = (long)maxValue - minValue;
            if (width <= Limit) return (int)(Next() * (1.0 / Limit) * width) + minValue;
            var signedSample = Next();
            if (Next() % 2 == 0) signedSample = -signedSample;
            var scaled = (signedSample + (double)(Limit - 1)) / (2.0 * Limit - 1.0);
            return (int)((long)(scaled * width) + minValue);
        }
    }
}
