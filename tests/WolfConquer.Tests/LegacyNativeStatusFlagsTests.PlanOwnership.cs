using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void PreparedWordCopiesCannotAlterStagedOrCommittedWords()
    {
        var flags = new Flags(64);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        lock (flags.SyncRoot)
        {
            Assert.True(flags.TryPrepareAdd(7, 11, true, 0, false, out var prepared));
            Assert.True(flags.TryGetPreparedWords(prepared, out var first));
            Assert.Equal(new uint[] { 192, 0 }, first);
            first[0] = 0;
            Assert.True(flags.TryGetPreparedWords(prepared, out var second));
            Assert.NotSame(first, second);
            Assert.Equal(new uint[] { 192, 0 }, second);
            Assert.True(prepared!.TryCommit(flags));
            Assert.NotSame(first, flags.bits);
            Assert.NotSame(second, flags.bits);
            second[0] = uint.MaxValue;
            Assert.Equal(new uint[] { 192, 0 }, flags.bits);
            Assert.True(flags.ContainFlag(6));
            Assert.True(flags.ContainFlag(7));
            Assert.False(flags.TryGetPreparedWords(prepared, out var unavailable));
            Assert.Empty(unavailable);
            var committed = CaptureVector(flags);
            Assert.False(prepared.TryCommit(flags));
            AssertVectorUnchanged(flags, committed);
        }
    }

    [Fact]
    public void ForgedDataFreeTokenHasNoStagedAuthority()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var before = CaptureVector(flags);
        var forged = new Flags.PreparedChange();
        lock (flags.SyncRoot)
        {
            Assert.False(flags.TryGetPreparedWords(forged, out var words));
            Assert.Empty(words);
            Assert.False(forged.TryCommit(flags));
        }
        AssertVectorUnchanged(flags, before);
    }

    [Fact]
    public void TokenCannotCommitThroughAnotherVectorEvenWhenRawPhysicalInputsAlias()
    {
        var owner = new Flags(32);
        Assert.True(owner.TryAdd(6, 7, false, 0));
        var other = new Flags(32) { ArrayFlags = owner.ArrayFlags, bits = owner.bits };
        Flags.PreparedChange? prepared;
        lock (owner.SyncRoot)
            Assert.True(owner.TryPrepareAdd(7, 11, false, 0, false, out prepared));
        var otherBefore = CaptureVector(other);
        lock (other.SyncRoot)
        {
            Assert.False(other.TryGetPreparedWords(prepared, out var words));
            Assert.Empty(words);
            Assert.False(prepared!.TryCommit(other));
        }
        AssertVectorUnchanged(other, otherBefore);
        lock (owner.SyncRoot) Assert.True(prepared!.TryCommit(owner));
        Assert.True(owner.ContainFlag(7));
        Assert.False(other.ContainFlag(7));
        Assert.Same(otherBefore.Dictionary, other.ArrayFlags);
        Assert.Same(otherBefore.Words, other.bits);
    }

    [Theory]
    [InlineData("dictionary")]
    [InlineData("bitmap")]
    [InlineData("row")]
    public void StaleAttemptConsumesTokenSoRestoringPriorRawInputsCannotReplayIt(string changed)
    {
        var flags = new Flags(64);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var dictionary = flags.ArrayFlags;
        var words = flags.bits;
        var row = flags.ArrayFlags[6];
        var seconds = row.Secounds;
        lock (flags.SyncRoot)
        {
            Assert.True(flags.TryPrepareAdd(7, 11, false, 0, false, out var prepared));
            if (changed == "dictionary") flags.ArrayFlags = new(dictionary);
            else if (changed == "bitmap") flags.bits = (uint[])words.Clone();
            else row.Secounds++;
            var stale = CaptureVector(flags);
            Assert.False(prepared!.TryCommit(flags));
            AssertVectorUnchanged(flags, stale);
            flags.ArrayFlags = dictionary;
            flags.bits = words;
            row.Secounds = seconds;
            var restored = CaptureVector(flags);
            Assert.False(flags.TryGetPreparedWords(prepared, out var unavailable));
            Assert.Empty(unavailable);
            Assert.False(prepared.TryCommit(flags));
            AssertVectorUnchanged(flags, restored);
            Assert.False(flags.ContainFlag(7));
        }
    }

    [Fact]
    public void PreparedWordReadAndCommitRequireTheGenuineVectorGate()
    {
        var flags = new Flags(32);
        Flags.PreparedChange? prepared;
        lock (flags.SyncRoot)
            Assert.True(flags.TryPrepareAdd(6, 7, false, 0, false, out prepared));
        var before = CaptureVector(flags);
        Assert.False(flags.TryGetPreparedWords(prepared, out var unavailable));
        Assert.Empty(unavailable);
        Assert.False(prepared!.TryCommit(flags));
        AssertVectorUnchanged(flags, before);
        lock (flags.SyncRoot)
        {
            Assert.True(flags.TryGetPreparedWords(prepared, out var words));
            Assert.Equal(new uint[] { 64 }, words);
            Assert.True(prepared.TryCommit(flags));
        }
        Assert.True(flags.ContainFlag(6));
    }
}
