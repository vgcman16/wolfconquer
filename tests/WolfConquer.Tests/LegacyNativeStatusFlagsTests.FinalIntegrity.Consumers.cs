using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    public static IEnumerable<object[]> FinalIntegrityConsumerCases()
    {
        foreach (string operation in new[] { "commit", "prepared-words", "update", "cache" })
            foreach (string drift in new[] { "seconds", "word", "registry", "row-key", "deadline", "invoke-deadline" })
                yield return new object[] { operation, drift };
    }

    [Theory]
    [MemberData(nameof(FinalIntegrityConsumerCases))]
    public void SharedCurrentGuardRejectsFinalComparerDriftBeforeStateCacheTimerOrPreparedOutput(string operation, string drift)
    {
        var fixture = new FinalIntegrityFixture(); var flags = fixture.Vector;
        Flags.PreparedChange? token = null;
        lock (flags.SyncRoot)
        {
            if (operation is "commit" or "prepared-words")
                Assert.True(flags.TryPrepareAdd(8, 13, true, 0, false, out token));
            int queried = fixture.Keys[^1];
            fixture.Arm(drift, fixture.Keys.Length + (operation == "update" ? 1 : 0));
            switch (operation)
            {
                case "commit":
                    Assert.False(token!.TryCommit(flags));
                    Assert.False(flags.TryGetPreparedWords(token, out var consumed)); Assert.Empty(consumed);
                    break;
                case "prepared-words":
                    Assert.False(flags.TryGetPreparedWords(token, out var unavailable)); Assert.Empty(unavailable);
                    break;
                case "update": Assert.False(flags.UpdateFlag(queried, 900, true, 999)); break;
                case "cache": Assert.Empty(flags.GetFlags()); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
            fixture.AssertOnlyStimulusRemains();
            var expected = operation == "update" ? new[] { queried }.Concat(fixture.Keys) : fixture.Keys;
            Assert.Equal(expected, fixture.Comparer.Reads);
            Assert.DoesNotContain(flags.ArrayFlags.ToArray(), pair => pair.Key == 8);
        }
    }

    public static IEnumerable<object[]> FinalIntegrityTimerCases()
    {
        foreach (string operation in new[] { "expire", "invoke" })
            foreach (string drift in FinalIntegrityDriftKinds)
                yield return new object[] { operation, drift };
    }

    [Theory]
    [MemberData(nameof(FinalIntegrityTimerCases))]
    public void TimerLookupCannotSucceedOrRenewAfterItsRealComparerCorruptsAnotherRegistration(string operation, string drift)
    {
        var fixture = new FinalIntegrityFixture(); int queried = fixture.Keys[^1];
        fixture.Arm(drift, 1);
        DateTime due = new DateTime(2000, 1, 1).AddSeconds(100);
        bool result = operation == "expire" ? fixture.Vector.InLife(queried, due) : fixture.Vector.CheckInvoke(queried, due);
        Assert.False(result);
        fixture.AssertOnlyStimulusRemains(); Assert.Equal(new[] { queried }, fixture.Comparer.Reads);
    }

    [Theory]
    [InlineData("expire")] [InlineData("invoke")] [InlineData("update")]
    public void TimerLookupCannotActOnADifferentLiteralPhysicalKeyReturnedByComparer(string operation)
    {
        var comparer = new FinalIntegrityAliasingLookupComparer();
        var flags = new Flags(64) { ArrayFlags = new(comparer) };
        Assert.True(flags.TryAdd(6, 7, false, 3));
        var row = flags.ArrayFlags[6]; row.Timer = row.TimerInvoker = new DateTime(2000, 1, 1);
        var before = CaptureVector(flags); comparer.Armed = true;
        DateTime due = new DateTime(2000, 1, 1).AddSeconds(100);
        bool result = operation == "expire" ? flags.InLife(18, due)
            : operation == "invoke" ? flags.CheckInvoke(18, due) : flags.UpdateFlag(18, 900, true, 999);
        Assert.False(result); Assert.Equal(1, comparer.Reads); AssertVectorUnchanged(flags, before);
    }

    [Theory]
    [InlineData("expire")] [InlineData("invoke-due")] [InlineData("invoke-not-due")]
    public void ValidTimerQueryKeepsGenuineExpiryAndInvocationTimingWithOneRealLookup(string operation)
    {
        var fixture = new FinalIntegrityFixture(); int queried = fixture.Keys[^1];
        var row = fixture.Vector.ArrayFlags[queried]; var prior = row.TimerInvoker;
        var before = DateTime.Now;
        fixture.Comparer.TriggerOnRead = int.MaxValue; fixture.Comparer.Armed = true;
        bool result = operation == "expire" ? fixture.Vector.InLife(queried, row.Timer.AddSeconds(row.Secounds)) :
            fixture.Vector.CheckInvoke(queried, prior.AddSeconds(row.InvokerSecouds).AddTicks(operation == "invoke-not-due" ? -1 : 0));
        var after = DateTime.Now;
        Assert.Equal(operation != "invoke-not-due", result); Assert.Equal(new[] { queried }, fixture.Comparer.Reads);
        if (operation == "invoke-due") Assert.InRange(row.TimerInvoker, before, after);
        else Assert.Equal(prior, row.TimerInvoker);
        Assert.Same(row, fixture.Vector.ArrayFlags.ToArray().Single(pair => pair.Key == queried).Value);
    }

    private sealed class FinalIntegrityAliasingLookupComparer : IEqualityComparer<int>
    {
        internal bool Armed;
        internal int Reads;
        public bool Equals(int left, int right) => left == right || Armed && (left == 6 && right == 18 || left == 18 && right == 6);
        public int GetHashCode(int value) { if (Armed) Reads++; return 0; }
    }
}
