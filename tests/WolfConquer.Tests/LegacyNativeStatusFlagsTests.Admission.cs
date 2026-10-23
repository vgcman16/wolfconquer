using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void MembershipTriStateSeparatesValidAbsencePresenceAndCorruption()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryContainFlag(6, out bool absent)); Assert.False(absent); Assert.False(flags.ContainFlag(6));
        Assert.True(flags.TryAdd(6, 7, false, 0));
        Assert.True(flags.TryContainFlag(6, out bool present)); Assert.True(present); Assert.True(flags.ContainFlag(6));
        flags.bits[0] = 0;
        Assert.False(flags.TryContainFlag(6, out bool unavailable)); Assert.False(unavailable);
        Assert.Throws<InvalidDataException>(() => flags.ContainFlag(6));
    }

    [Theory]
    [InlineData("dictionary")] [InlineData("words")] [InlineData("bit-only")]
    [InlineData("row-only")] [InlineData("row-key")] [InlineData("null-row")]
    [InlineData("alias-row")] [InlineData("bad-expiry")] [InlineData("bad-invoke")]
    public void MalformedFullGraphRefusesAllCooperatingMutationsWithoutRepairingAPrefix(string malformed)
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(6, 7, true, 3));
        if (malformed == "dictionary") flags.ArrayFlags = null!;
        else if (malformed == "words") flags.bits = null!;
        else if (malformed == "bit-only") flags.Add(7);
        else if (malformed == "row-only") flags.ArrayFlags[7] = new(7, 10, false, 0);
        else if (malformed == "row-key") flags.ArrayFlags[6].Key = 7;
        else if (malformed == "null-row") { flags.ArrayFlags[7] = null!; flags.Add(7); }
        else if (malformed == "alias-row") { flags.ArrayFlags[7] = flags.ArrayFlags[6]; flags.Add(7); }
        else if (malformed == "bad-expiry") flags.ArrayFlags[6].Timer = DateTime.MaxValue;
        else flags.ArrayFlags[6].TimerInvoker = DateTime.MaxValue;
        var before = CaptureVector(flags);
        Assert.False(flags.TryAdd(8, 11, false, 0)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.TryReplace(6, 11, false, 0)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.TryRemove(6)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.TryClearDeath()); AssertVectorUnchanged(flags, before);
        flags.GetClear(); AssertVectorUnchanged(flags, before);
        Assert.False(flags.UpdateFlag(6, 10, true, 100)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.CheckInvoke(6, DateTime.MaxValue)); AssertVectorUnchanged(flags, before);
        Assert.False(flags.InLife(6, DateTime.MaxValue));
        Assert.False(flags.TryContainFlag(6, out bool present)); Assert.False(present);
        Assert.Throws<InvalidDataException>(() => flags.ContainFlag(6));
        Assert.Empty(flags.GetFlags()); AssertVectorUnchanged(flags, before);
    }

    [Fact]
    public void DictionaryReplacementIsValidatedAgainstTheEntireBitmapRatherThanAcceptedAsCacheAuthority()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(1, 7, false, 0));
        var old = flags.GetFlags(); flags.ArrayFlags = new(); flags.ArrayFlags[3] = new(3, 11, false, 0);
        var invalid = CaptureVector(flags);
        Assert.False(flags.TryAdd(4, 13, false, 0)); Assert.Empty(flags.GetFlags());
        AssertVectorUnchanged(flags, invalid); Assert.Equal(1, Assert.Single(old).Key);
        // A deterministic raw migration is not an atomic writer API; once coherent, reads use current rows.
        flags.bits = new uint[] { 8, 0 };
        var current = Assert.Single(flags.GetFlags()); Assert.Same(flags.ArrayFlags[3], current);
        Assert.True(flags.TryAdd(4, 13, false, 0)); Assert.Same(current, flags.ArrayFlags[3]);
    }

    [Fact]
    public void TryReplaceStagesNewRowAndRetainsUnrelatedRealRegistrationAndTimers()
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(6, 7, true, 3));
        Assert.True(flags.TryAdd(7, 9, false, 0));
        var old = flags.ArrayFlags[6]; var retained = flags.ArrayFlags[7];
        var timer = retained.Timer; var invoke = retained.TimerInvoker;
        var now = DateTime.Now; Assert.True(flags.TryReplace(6, -9, false, -7));
        var after = DateTime.Now; var replacement = flags.ArrayFlags[6];
        Assert.NotSame(old, replacement); Assert.Equal(7, old.Secounds); Assert.True(old.RemoveOnDead);
        Assert.Equal(-9, replacement.Secounds); Assert.Equal(-7, replacement.InvokerSecouds);
        Assert.False(replacement.RemoveOnDead); Assert.InRange(replacement.Timer, now, after);
        Assert.InRange(replacement.TimerInvoker, now, after);
        Assert.Same(retained, flags.ArrayFlags[7]); Assert.Equal(timer, retained.Timer);
        Assert.Equal(invoke, retained.TimerInvoker); Assert.Equal(new uint[] { 192, 0 }, flags.bits);
    }

    [Theory]
    [InlineData("dictionary")] [InlineData("bitmap")] [InlineData("row")]
    public void PreparedRegistrationRejectsStalePhysicalInputsBeforePublishingAnyStagedReference(string changed)
    {
        var flags = new Flags(64); Assert.True(flags.TryAdd(6, 7, true, 3));
        lock (flags.SyncRoot)
        {
            Assert.True(flags.TryPrepareAdd(7, 11, false, 0, false, out var prepared));
            if (changed == "dictionary") flags.ArrayFlags = new(flags.ArrayFlags);
            else if (changed == "bitmap") flags.bits[0] |= 1u << 8;
            else flags.ArrayFlags[6].Secounds++;
            var before = CaptureVector(flags);
            Assert.False(prepared!.TryCommit(flags)); AssertVectorUnchanged(flags, before);
            Assert.False(flags.ArrayFlags.ContainsKey(7));
        }
    }

    [Fact]
    public void PreparingOrCommittingOutsideTheVectorGateCannotMutateState()
    {
        var flags = new Flags(32); var before = CaptureVector(flags);
        Assert.False(flags.TryPrepareAdd(6, 10, false, 0, false, out var missing)); Assert.Null(missing);
        Flags.PreparedChange? prepared;
        lock (flags.SyncRoot) Assert.True(flags.TryPrepareAdd(6, 10, false, 0, false, out prepared));
        Assert.False(prepared!.TryCommit(flags)); AssertVectorUnchanged(flags, before);
    }

    [Fact]
    public void RealDictionaryComparerFailureDuringCowMaterializationRefusesUnchanged()
    {
        var comparer = new ArmedComparer();
        var flags = new Flags(64) { ArrayFlags = new(comparer) };
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var before = CaptureVector(flags); comparer.Armed = true;
        try
        {
            Assert.False(flags.TryAdd(7, 11, true, 0));
            Assert.Equal(new[] { 7, 6 }, comparer.ArmedHashQueries);
        }
        finally { comparer.Armed = false; }
        AssertVectorUnchanged(flags, before); Assert.False(flags.ArrayFlags.ContainsKey(7));
    }

    private sealed class ArmedComparer : IEqualityComparer<int>
    {
        internal bool Armed;
        public bool Equals(int first, int second) => first == second;
        internal List<int> ArmedHashQueries { get; } = [];
        public int GetHashCode(int value)
        {
            if (!Armed) return value;
            ArmedHashQueries.Add(value);
            // New-key preflight succeeds; cloning the existing key6 is the refusal point.
            if (value == 6) throw new InvalidOperationException("owned clone fault");
            return value;
        }
    }
}
