using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void ConstructorHasDistinctEmptyRegistrationBitAndCacheOwners()
    {
        var first = new Flags(576);
        var second = new Flags(576);
        Assert.Equal(576, first.Size);
        Assert.Equal(new uint[18], first.bits);
        Assert.Empty(first.ArrayFlags);
        Assert.Empty(first.GetFlags());
        Assert.Same(first.GetFlags(), first.GetFlags());
        Assert.NotSame(first.GetFlags(), second.GetFlags());
        Assert.NotSame(first.ArrayFlags, second.ArrayFlags);
        Assert.NotSame(first.bits, second.bits);
        Assert.Equal(2592000, Flags.PermanentFlag);
        Assert.Equal(604800, Flags.PermanentFlag2);
        Assert.Equal(86400, Flags.PermanentFlag3);
    }

    [Fact]
    public void DictionaryContainmentIsSeparateFromBitsAndDuplicateAddDoesNotRepairBits()
    {
        var flags = new Flags(64);
        flags.Add(6);
        Assert.True(flags.Contain(6));
        Assert.False(flags.ContainFlag(6));
        Assert.True(flags.TryAdd(6, 7, false, 3));
        var registration = flags.ArrayFlags[6];
        var timer = registration.Timer;
        var invokerTimer = registration.TimerInvoker;
        flags.Remove(6);
        Assert.False(flags.Contain(6));
        Assert.True(flags.ContainFlag(6));
        Assert.False(flags.TryAdd(6, 999, true, 99));
        Assert.Same(registration, flags.ArrayFlags[6]);
        Assert.Equal(timer, registration.Timer);
        Assert.Equal(invokerTimer, registration.TimerInvoker);
        Assert.Equal(7, registration.Secounds);
        Assert.Equal(3, registration.InvokerSecouds);
        Assert.False(registration.RemoveOnDead);
        Assert.False(flags.Contain(6));
        Assert.True(flags.TryRemove(6));
        Assert.False(flags.TryRemove(6));
        Assert.False(flags.ContainFlag(6));
    }

    [Fact]
    public void PublicDictionaryReplacementAndDirectMutationLeaveCachedArraysStale()
    {
        var flags = new Flags(64);
        var empty = flags.GetFlags();
        var direct = new Flags.Flag(1, 7, false, 0);
        flags.ArrayFlags[1] = direct;
        Assert.True(flags.ContainFlag(1));
        Assert.False(flags.Contain(1));
        Assert.Same(empty, flags.GetFlags());
        Assert.True(flags.TryAdd(2, 9, false, 0));
        var oldCache = flags.GetFlags();
        Assert.Equal(2, oldCache.Length);
        Assert.Contains(oldCache, x => ReferenceEquals(x, direct));
        var replacement = new Flags.Flag(3, 11, false, 0);
        flags.ArrayFlags = new ConcurrentDictionary<int, Flags.Flag>();
        flags.ArrayFlags[3] = replacement;
        Assert.True(flags.ContainFlag(3));
        Assert.False(flags.ContainFlag(1));
        Assert.Same(oldCache, flags.GetFlags());
        Assert.True(flags.TryAdd(4, 13, false, 0));
        var refreshed = flags.GetFlags();
        Assert.NotSame(oldCache, refreshed);
        Assert.Equal(2, refreshed.Length);
        Assert.Contains(refreshed, x => ReferenceEquals(x, replacement));
        Assert.DoesNotContain(refreshed, x => ReferenceEquals(x, direct));
        Assert.True(flags.Contain(2)); // Replacing the dictionary never rewrites the bits.
    }

    [Fact]
    public void GetClearRemovesOnlyDeathMarkedRegistrationsNotExpiredOthers()
    {
        var flags = new Flags(64);
        flags.TryAdd(6, 0, false, 0);
        flags.TryAdd(7, 7, true, 0);
        flags.TryAdd(8, 8, false, 0);
        var retained = flags.ArrayFlags[6];
        retained.Timer = new DateTime(2000, 1, 1);
        Assert.True(flags.InLife(6, DateTime.Now));
        flags.GetClear();
        Assert.True(flags.ContainFlag(6));
        Assert.True(flags.Contain(6));
        Assert.False(flags.ContainFlag(7));
        Assert.False(flags.Contain(7));
        Assert.True(flags.ContainFlag(8));
        Assert.Same(retained, flags.ArrayFlags[6]);
        Assert.Equal(2, flags.GetFlags().Length);
    }

    [Fact]
    public void GetClearUsesTheExposedCacheAndValueKeysRatherThanDictionaryPolicy()
    {
        var flags = new Flags(64);
        flags.TryAdd(1, 7, false, 0);
        flags.TryAdd(2, 7, false, 0);
        var cache = flags.GetFlags();
        cache[0] = new Flags.Flag(2, 7, true, 0);
        Assert.False(flags.ArrayFlags[2].RemoveOnDead);
        flags.GetClear();
        Assert.True(flags.ContainFlag(1));
        Assert.False(flags.ContainFlag(2));
        flags.ArrayFlags[1].Key = 99;
        flags.ArrayFlags[1].RemoveOnDead = true;
        flags.GetClear(); // Attempts removal by value.Key99, not its actual dictionary key1.
        Assert.True(flags.ContainFlag(1));
        Assert.True(flags.Contain(1));
    }

    [Fact]
    public void NegativeBitFaultPublishesThenRemovesRegistrationBeforeCacheDirtying()
    {
        var flags = new Flags(32);
        var empty = flags.GetFlags();
        Assert.Throws<IndexOutOfRangeException>(() => flags.TryAdd(-32, 7, true, 0));
        Assert.True(flags.ContainFlag(-32));
        Assert.Equal(-32, flags.ArrayFlags[-32].Key);
        Assert.Equal(new uint[1], flags.bits);
        Assert.Same(empty, flags.GetFlags());
        Assert.Throws<IndexOutOfRangeException>(() => flags.TryRemove(-32));
        Assert.False(flags.ContainFlag(-32));
        Assert.Same(empty, flags.GetFlags());
    }

    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    public void HighIndicesStillRegisterEvenWhenTheBaseBitAddIsIgnored(int index)
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(index, 7, false, 0));
        Assert.True(flags.ContainFlag(index));
        Assert.Equal(new uint[1], flags.bits);
        Assert.Same(flags.ArrayFlags[index], Assert.Single(flags.GetFlags()));
        Assert.True(flags.TryRemove(index));
        Assert.Empty(flags.GetFlags());
    }
}
