using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[CollectionDefinition("NativeFamily8418Rate", DisableParallelization = true)]
public sealed class NativeFamily8418RateCollection { }

[Collection("NativeFamily8418Rate")]
public sealed class NativeFamily8418RateTests
{
    [Fact]
    public void NativeDoubleZeroDoesNotDraw()
    {
        var saved = LegacyNativePoolRandom.GetRandom;
        try
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(913);
            Assert.False(InvokeRate(0));
            Assert.Equal(new Random(913).Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        finally { LegacyNativePoolRandom.GetRandom = saved; }
    }

    [Fact]
    public void NativeThreeEMinusFiveRoundsToZeroButStillConsumesOneLivePoolDraw()
    {
        var saved = LegacyNativePoolRandom.GetRandom;
        try
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(913);
            var expected = new Random(913);
            expected.Next();
            Assert.False(InvokeRate(3E-05));
            Assert.Equal(expected.Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        finally { LegacyNativePoolRandom.GetRandom = saved; }
    }

    [Fact]
    public void NativeFractionUsesIntegerDiscriminantNotPercentageAndRereadsPoolBetweenCalls()
    {
        var saved = LegacyNativePoolRandom.GetRandom;
        try
        {
            foreach (var seed in new[] { 913, 4_105 })
            {
                LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
                var expected = new Random(seed);
                Assert.Equal(8 > expected.Next() % 1_000, InvokeRate(0.008));
                Assert.Equal(expected.Next(), LegacyNativePoolRandom.GetRandom.Next());
            }
        }
        finally { LegacyNativePoolRandom.GetRandom = saved; }
    }

    private static bool InvokeRate(double value) =>
        (bool)typeof(WorldPacketHandlers).GetMethod("CheckLegacyFamily8418NativeRate",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [value])!;
}
