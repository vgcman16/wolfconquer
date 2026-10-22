using System.Reflection;
using Vector = WolfConquer.Core.World.LegacyNativeBitVector32;

namespace WolfConquer.Tests;

public sealed class LegacyNativeBitVectorTests
{
    [Fact]
    public void ConstructorRoundsUsingNativeSignedDivisionAndExposesAllocatedCapacity()
    {
        foreach (var (requested, words) in new[]
        {
            (0, 0), (1, 1), (31, 1), (32, 1), (33, 2), (63, 2), (64, 2), (65, 3),
            (-1, 1), (-31, 1), (-33, 0), (-63, 0)
        })
        {
            var vector = new Vector(requested);
            Assert.Equal(words, vector.bits.Length);
            Assert.Equal(words * 32, vector.Size);
            Assert.All(vector.bits, word => Assert.Equal(0u, word));
        }
        var field = typeof(Vector).GetField("bits", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.Equal(typeof(uint[]), field.FieldType);
        Assert.False(field.IsInitOnly);
        Assert.Null(typeof(Vector).GetProperty("Size")!.SetMethod);
    }

    [Fact]
    public void NegativeArrayLengthCtorCasesThrowTheActualManagedAllocationFault()
    {
        foreach (int requested in new[] { -32, -64, -65, int.MinValue })
            Assert.Throws<OverflowException>(() => new Vector(requested));
    }

    [Fact]
    public void AddRemoveAreIdempotentAndUseRoundedCapacityRatherThanRequestedBitCount()
    {
        var vector = new Vector(1);
        vector.Add(0);
        vector.Add(31);
        vector.Add(31);
        Assert.Equal(new uint[] { 0x80000001 }, vector.bits);
        Assert.True(vector.Contain(0));
        Assert.True(vector.Contain(31));
        vector.Remove(31);
        vector.Remove(31);
        Assert.Equal(new uint[] { 1 }, vector.bits);
        Assert.False(vector.Contain(31));
        Assert.True(vector.Contain(0));
        vector.Remove(0);
        Assert.Equal(0u, Assert.Single(vector.bits));
    }

    [Fact]
    public void ExactCapacityIsIgnoredByMutationButNotRejectedByContain()
    {
        foreach (int capacity in new[] { 0, 32, 64 })
        {
            var vector = new Vector(capacity);
            Array.Fill(vector.bits, uint.MaxValue);
            var before = vector.bits.ToArray();
            vector.Add(capacity);
            vector.Remove(capacity);
            vector.Add(int.MaxValue);
            vector.Remove(int.MaxValue);
            Assert.Equal(before, vector.bits);
            Assert.Throws<IndexOutOfRangeException>(() => vector.Contain(capacity));
            Assert.False(vector.Contain(capacity + 1));
            Assert.False(vector.Contain(int.MaxValue));
            Assert.Equal(before, vector.bits);
        }
    }

    [Fact]
    public void NegativeIndicesInTheFirstTruncatedWordAliasMaskedShiftPositions()
    {
        foreach (var (negative, positive, mask) in new[]
        {
            (-1, 31, 0x80000000u), (-2, 30, 0x40000000u), (-31, 1, 2u)
        })
        {
            var vector = new Vector(32);
            vector.Add(0);
            vector.Add(negative);
            Assert.Equal(1u | mask, vector.bits[0]);
            Assert.True(vector.Contain(negative));
            Assert.True(vector.Contain(positive));
            vector.Remove(negative);
            Assert.Equal(1u, vector.bits[0]);
            Assert.False(vector.Contain(negative));
            Assert.False(vector.Contain(positive));
        }
        foreach (int index in new[] { -32, -33, -63, int.MinValue })
        {
            var vector = new Vector(32);
            Assert.Throws<IndexOutOfRangeException>(() => vector.Add(index));
            Assert.Throws<IndexOutOfRangeException>(() => vector.Remove(index));
            Assert.Throws<IndexOutOfRangeException>(() => vector.Contain(index));
            Assert.Equal(0u, vector.bits[0]);
        }
        var empty = new Vector(0);
        Assert.Throws<IndexOutOfRangeException>(() => empty.Add(-1));
        Assert.Throws<IndexOutOfRangeException>(() => empty.Remove(-1));
        Assert.Throws<IndexOutOfRangeException>(() => empty.Contain(-1));
    }

    [Fact]
    public void CountIgnoresBit31InEveryUIntWordDespiteContainSeeingIt()
    {
        var vector = new Vector(0) { bits = [0x80000000, uint.MaxValue, 0x7FFFFFFF, 0x40000000] };
        Assert.Equal(128, vector.Size);
        Assert.True(vector.Contain(31));
        Assert.True(vector.Contain(63));
        Assert.Equal(63, vector.Count()); // 0 +31 +31 +1, not a corrected unsigned popcount.
        vector.Remove(31);
        vector.Remove(63);
        Assert.Equal(63, vector.Count());
        vector.Remove(30);
        Assert.Equal(63, vector.Count()); // Word0 had no bit30.
        vector.Remove(62);
        Assert.Equal(62, vector.Count());
        vector.Clear();
        Assert.Equal(0, vector.Count());
    }

    [Fact]
    public void PublicArrayReplacementImmediatelyChangesSizeAndRetainsExternalAliases()
    {
        var vector = new Vector(96);
        var original = vector.bits;
        uint[] replacement = [0, 0];
        vector.bits = replacement;
        Assert.Same(replacement, vector.bits);
        Assert.Equal(64, vector.Size);
        vector.Add(63);
        vector.Add(0);
        Assert.Equal(new uint[] { 1, 0x80000000 }, replacement);
        Assert.All(original, word => Assert.Equal(0u, word));
        replacement[1] |= 1;
        Assert.True(vector.Contain(32));
        Assert.Equal(2, vector.Count());
        vector.Clear();
        Assert.Same(replacement, vector.bits);
        Assert.All(replacement, word => Assert.Equal(0u, word));
        vector.bits = [];
        Assert.Equal(0, vector.Size);
        Assert.Equal(0, vector.Count());
    }

    [Fact]
    public void NullPublicArrayFaultsAtSizeBeforeAnyIndexCanShortCircuitIt()
    {
        var vector = new Vector(32) { bits = null! };
        Assert.Throws<NullReferenceException>(() => { _ = vector.Size; });
        Assert.Throws<NullReferenceException>(() => vector.Add(int.MaxValue));
        Assert.Throws<NullReferenceException>(() => vector.Remove(int.MaxValue));
        Assert.Throws<NullReferenceException>(() => vector.Contain(int.MaxValue));
        Assert.Throws<NullReferenceException>(() => vector.Count());
        Assert.Throws<NullReferenceException>(() => vector.Clear());
        Assert.Null(vector.bits);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(255, 255)]
    [InlineData(256, 0)]
    [InlineData(257, 1)]
    public void ClearNarrowsTheWordCountToAByteAndTerminatesWithoutRepair(int words, int cleared)
    {
        var vector = new Vector(words * 32);
        var sameArray = vector.bits;
        Array.Fill(vector.bits, uint.MaxValue);

        vector.Clear();

        Assert.Same(sameArray, vector.bits);
        Assert.Equal(words * 32, vector.Size);
        for (int index = 0; index < words; index++)
            Assert.Equal(index < cleared ? 0u : uint.MaxValue, vector.bits[index]);
        Assert.Equal(31 * (words - cleared), vector.Count());
    }
}
