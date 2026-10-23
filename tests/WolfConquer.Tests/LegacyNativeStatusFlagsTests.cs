using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void ConstructorHasDistinctEmptyOwnersAndReturnsDefensiveCacheArrays()
    {
        var first = new Flags(576); var second = new Flags(576);
        Assert.Equal(576, first.Size); Assert.Equal(new uint[18], first.bits);
        Assert.Empty(first.ArrayFlags); Assert.Empty(first.GetFlags());
        Assert.NotSame(first.GetFlags(), first.GetFlags());
        Assert.NotSame(first.ArrayFlags, second.ArrayFlags); Assert.NotSame(first.bits, second.bits);
        Assert.Equal(2592000, Flags.PermanentFlag); Assert.Equal(604800, Flags.PermanentFlag2);
        Assert.Equal(86400, Flags.PermanentFlag3);
    }

    [Fact]
    public void CoherentDuplicateRefusesWithoutChangingDictionaryWordsRowsOrTimers()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(6, 7, false, 3));
        var before = CaptureVector(flags);
        Assert.False(flags.TryAdd(6, 999, true, 99)); AssertVectorUnchanged(flags, before);
        Assert.True(flags.ContainFlag(6)); Assert.True(flags.Contain(6));
        Assert.True(flags.TryRemove(6)); Assert.False(flags.TryRemove(6));
        Assert.False(flags.ContainFlag(6)); Assert.False(flags.Contain(6));
    }

    [Fact]
    public void ExposedCacheEditsCannotInjectOrRemoveAuthoritativeRegistrations()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(1, 7, false, 0));
        Assert.True(flags.TryAdd(2, 7, false, 0));
        var one = flags.ArrayFlags[1]; var two = flags.ArrayFlags[2];
        var escaped = flags.GetFlags(); escaped[0] = new Flags.Flag(2, 7, true, 0);
        escaped[1] = null!;
        Assert.True(flags.TryClearDeath());
        Assert.Same(one, flags.ArrayFlags[1]); Assert.Same(two, flags.ArrayFlags[2]);
        Assert.False(one.RemoveOnDead); Assert.False(two.RemoveOnDead);
        var refreshed = flags.GetFlags(); Assert.NotSame(escaped, refreshed);
        Assert.Equal(2, refreshed.Length); Assert.Contains(one, refreshed); Assert.Contains(two, refreshed);
    }

    [Fact]
    public void GetClearRemovesOnlyAuthoritativeDeathMarkedRowsNotExpiredOthers()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(6, 0, false, 0));
        Assert.True(flags.TryAdd(7, 7, true, 0)); Assert.True(flags.TryAdd(8, 8, false, 0));
        var retained = flags.ArrayFlags[6]; retained.Timer = new DateTime(2000, 1, 1);
        Assert.True(flags.InLife(6, DateTime.Now)); flags.GetClear();
        Assert.True(flags.ContainFlag(6)); Assert.True(flags.Contain(6));
        Assert.False(flags.ContainFlag(7)); Assert.False(flags.Contain(7));
        Assert.True(flags.ContainFlag(8)); Assert.Same(retained, flags.ArrayFlags[6]);
        Assert.Equal(2, flags.GetFlags().Length);
    }

    [Fact]
    public void SuccessfulCowMutationRetainsUntouchedRowIdentityAndOldSnapshotContents()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(1, 7, false, 3));
        var oldDictionary = flags.ArrayFlags; var oldWords = flags.bits;
        var oldCache = flags.GetFlags(); var one = flags.ArrayFlags[1];
        var time = one.Timer; var invoke = one.TimerInvoker;
        Assert.True(flags.TryAdd(33, -7, true, -3));
        Assert.NotSame(oldDictionary, flags.ArrayFlags); Assert.NotSame(oldWords, flags.bits);
        Assert.Single(oldDictionary); Assert.Equal(new uint[] { 2, 0 }, oldWords);
        Assert.Same(one, Assert.Single(oldCache)); Assert.Same(one, flags.ArrayFlags[1]);
        Assert.Equal(time, one.Timer); Assert.Equal(invoke, one.TimerInvoker);
        Assert.Equal(new uint[] { 2, 2 }, flags.bits); Assert.True(flags.ContainFlag(33));
        Assert.True(flags.TryRemove(33)); Assert.Same(one, flags.ArrayFlags[1]);
    }

    [Theory]
    [InlineData(-33)] [InlineData(-32)] [InlineData(-1)] [InlineData(32)] [InlineData(33)] [InlineData(int.MaxValue)]
    public void OutOfRangeFlagRefusesBeforeAnyRegistrationOrBitmapMutation(int index)
    {
        var flags = new Flags(32); Assert.True(flags.TryAdd(6, 7, false, 0));
        var before = CaptureVector(flags);
        Assert.False(flags.TryAdd(index, 7, false, 0)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.TryReplace(index, 7, false, 0)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.TryRemove(index)); AssertVectorUnchanged(flags, before);
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(32)] [InlineData(63)]
    public void PhysicalWordBoundariesAdmitExactlyTheirRealBit(int index)
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(index, int.MinValue, false, int.MaxValue));
        var expected = new uint[2]; expected[index / 32] = 1u << (index % 32);
        Assert.Equal(expected, flags.bits); Assert.True(flags.ContainFlag(index));
        var row = Assert.Single(flags.GetFlags()); Assert.Equal(index, row.Key);
        Assert.Equal(int.MinValue, row.Secounds); Assert.Equal(int.MaxValue, row.InvokerSecouds);
    }

    [Fact]
    public void ZeroCapacityRefusesFlagButSupportsValidEmptyReadsAndDeathClear()
    {
        var flags = new Flags(0); Assert.Equal(0, flags.Size);
        Assert.False(flags.TryAdd(0, 1, false, 0)); Assert.True(flags.TryClearDeath());
        Assert.Empty(flags.GetFlags()); Assert.Empty(flags.ArrayFlags); Assert.Empty(flags.bits);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Flags(-1));
    }

    private sealed record SavedRow(Flags.Flag Value, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool Death);
    private sealed record VectorState(object? Dictionary, KeyValuePair<int, Flags.Flag>[]? Entries,
        uint[]? Words, uint[]? Bytes, SavedRow[] Rows);

    private static VectorState CaptureVector(Flags flags)
    {
        var entries = flags.ArrayFlags?.ToArray();
        var rows = (entries ?? []).Where(pair => pair.Value is not null).Select(pair => pair.Value)
            .Distinct(ReferenceEqualityComparer.Instance).Cast<Flags.Flag>()
            .Select(row => new SavedRow(row, row.Key, row.Secounds, row.InvokerSecouds,
                row.Timer, row.TimerInvoker, row.RemoveOnDead)).ToArray();
        return new(flags.ArrayFlags, entries, flags.bits, flags.bits?.ToArray(), rows);
    }

    private static void AssertVectorUnchanged(Flags flags, VectorState before)
    {
        Assert.Same(before.Dictionary, flags.ArrayFlags); Assert.Same(before.Words, flags.bits);
        Assert.Equal(before.Bytes, flags.bits); Assert.Equal(before.Entries, flags.ArrayFlags?.ToArray());
        foreach (var row in before.Rows)
        {
            Assert.Equal(row.Key, row.Value.Key); Assert.Equal(row.Seconds, row.Value.Secounds);
            Assert.Equal(row.Invoker, row.Value.InvokerSecouds); Assert.Equal(row.Timer, row.Value.Timer);
            Assert.Equal(row.InvokeTimer, row.Value.TimerInvoker); Assert.Equal(row.Death, row.Value.RemoveOnDead);
        }
    }
}
