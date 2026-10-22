using System.Collections.Concurrent;
using WolfConquer.Core.World;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

// Actual mutable owners/arrays; concurrent interleavings are source-reviewed, not race-hooked.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearValueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnloadedGatePrecedesEvenNullInfoAndNullKeyWithoutRepairOrMutation(bool nullInfo)
    {
        using var load = new LoadScope(false);
        var gear = new LegacyNativeCombatGear();
        var prior = gear.Info;
        double[] retained = [7];
        prior["retained"] = retained;
        if (nullInfo) gear.Info = null!;

        gear.SetValue(null!, (double[])null!);
        gear.SetValue("retained", 8, 9);

        Assert.Null(gear.GetValue(null!));
        Assert.Null(gear.GetValue("retained"));
        Assert.Same(retained, prior["retained"]);
        Assert.Equal(7, retained[0]);
        if (nullInfo) Assert.Null(gear.Info);
        else Assert.Same(prior, gear.Info);
        Assert.False(GearTable.m_Load);
    }

    [Fact]
    public void AddedArrayAndReturnedArrayAreTheExactCallerOwnedMutableReference()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        var dictionary = gear.Info;
        double[] values = [1, 2];

        gear.SetValue("attack", values);

        Assert.Same(dictionary, gear.Info);
        Assert.Same(values, Assert.Single(dictionary).Value);
        Assert.Same(values, gear.GetValue("attack"));
        values[0] = 17;
        Assert.Equal(17, gear.GetValue("attack")[0]);
        gear.GetValue("attack")[1] = 31;
        Assert.Equal(31, values[1]);
        Assert.True(GearTable.m_Load);
    }

    [Fact]
    public void ExistingEntryIsReplacedWithTheNewAliasWithoutMutatingTheRetainedOldArray()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        double[] old = [5, 7];
        double[] replacement = [11];
        gear.SetValue("power", old);
        var retained = gear.GetValue("power");

        gear.SetValue("power", replacement);

        Assert.Same(replacement, gear.Info["power"]);
        Assert.Same(replacement, gear.GetValue("power"));
        Assert.Same(old, retained);
        Assert.Equal(new double[] { 5, 7 }, retained);
        double[] empty = [];
        gear.SetValue("power", empty);
        Assert.Same(empty, gear.GetValue("power"));
        Assert.Single(gear.Info);
    }

    [Fact]
    public void UnloadedNullAndLoadedMissingFreshEmptyArraysAreDifferentContracts()
    {
        using var load = new LoadScope(false);
        var gear = new LegacyNativeCombatGear();
        var dictionary = gear.Info;
        Assert.Null(gear.GetValue("missing"));
        GearTable.m_Load = true;

        var first = gear.GetValue("missing");
        var second = gear.GetValue("missing");

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.Same(dictionary, gear.Info);
        Assert.Empty(dictionary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullDataCanBeAddedOrReplaceAnExistingValueAndRemainsACachedNull(bool existing)
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        double[] retained = [3];
        if (existing) gear.Info["value"] = retained;

        gear.SetValue("value", (double[])null!);

        Assert.True(gear.Info.ContainsKey("value"));
        Assert.Null(gear.Info["value"]);
        Assert.Null(gear.GetValue("value"));
        Assert.Single(gear.Info);
        Assert.Equal(3, retained[0]);
        Assert.Empty(gear.GetValue("different"));
    }

    [Fact]
    public void LoadedNullKeyFaultsInTheRealDictionaryBeforeAnyDataPublication()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        var dictionary = gear.Info;

        var write = Assert.Throws<ArgumentNullException>(() => gear.SetValue(null!, (double[])null!));
        var read = Assert.Throws<ArgumentNullException>(() => gear.GetValue(null!));

        Assert.Equal("key", write.ParamName);
        Assert.Equal("key", read.ParamName);
        Assert.Empty(dictionary);
        Assert.Same(dictionary, gear.Info);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedNullInfoFaultsBeforeKeyValidationAndNeverCreatesAFallback(bool nullKey)
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear { Info = null! };
        var key = nullKey ? null! : "value";

        Assert.Throws<NullReferenceException>(() => gear.SetValue(key, 1));
        Assert.Throws<NullReferenceException>(() => gear.GetValue(key));

        Assert.Null(gear.Info);
        Assert.True(GearTable.m_Load);
    }

    [Fact]
    public void EveryCallUsesTheCurrentInfoOwnerAndItsActualComparer()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        var old = gear.Info;
        double[] original = [3];
        double[] current = [5];
        double[] replacement = [7];
        old["Power"] = original;
        var next = new ConcurrentDictionary<string, double[]>(StringComparer.OrdinalIgnoreCase) { ["Power"] = current };
        gear.Info = next;

        Assert.Same(current, gear.GetValue("power"));
        gear.SetValue("power", replacement);

        Assert.Same(next, gear.Info);
        Assert.Same(replacement, gear.GetValue("POWER"));
        Assert.Same(replacement, next["Power"]);
        Assert.Same(original, old["Power"]);
        Assert.False(old.ContainsKey("power"));
        Assert.Single(next);
        Assert.Single(old);
    }

    [Fact]
    public void NativeDefaultDictionaryKeepsEmptyWhitespaceAndCaseDistinctKeys()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        var keys = new[] { "", " ", "Power", "power" };
        var values = keys.Select((_, index) => new[] { (double)index }).ToArray();

        for (var i = 0; i < keys.Length; i++) gear.SetValue(keys[i], values[i]);

        Assert.Equal(4, gear.Info.Count);
        for (var i = 0; i < keys.Length; i++) Assert.Same(values[i], gear.GetValue(keys[i]));
    }

    [Fact]
    public void NaNPayloadInfinitiesAndNegativeZeroRemainBitExactWithoutNormalization()
    {
        using var load = new LoadScope();
        var gear = new LegacyNativeCombatGear();
        long[] bits =
        [
            0x7FF8000000000042L,
            0x7FF0000000000000L,
            unchecked((long)0xFFF0000000000000UL),
            unchecked((long)0x8000000000000000UL),
            0L
        ];
        var values = bits.Select(BitConverter.Int64BitsToDouble).ToArray();

        gear.SetValue("raw", values);

        Assert.Same(values, gear.GetValue("raw"));
        Assert.Equal(bits, gear.GetValue("raw").Select(BitConverter.DoubleToInt64Bits));
    }

    private sealed class LoadScope : IDisposable
    {
        private readonly bool prior = GearTable.m_Load;
        internal LoadScope(bool loaded = true) => GearTable.m_Load = loaded;
        public void Dispose() => GearTable.m_Load = prior;
    }
}
