using System.Collections.Concurrent;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    public static IEnumerable<object[]> FinalIntegrityQueryCases()
    {
        foreach (string phase in new[] { "lookup", "membership" })
            foreach (bool physicallyPresent in new[] { false, true })
                foreach (string drift in FinalIntegrityDriftKinds)
                    yield return new object[] { phase, physicallyPresent, drift };
    }

    [Theory]
    [MemberData(nameof(FinalIntegrityQueryCases))]
    public void LateComparerCorruptionCannotAuthorizePositiveOrNegatedMembership(string phase, bool physicallyPresent, string drift)
    {
        // Exercise tri-state and legacy bool separately from the same valid prestate:
        // a later independent query may legitimately observe a coherent raw migration.
        foreach (bool legacy in new[] { false, true })
        {
            var fixture = new FinalIntegrityFixture();
            int queried = physicallyPresent ? fixture.Keys[0] : 18;
            fixture.Arm(drift, fixture.Keys.Length + (phase == "membership" ? 1 : 0));
            if (legacy)
            {
                bool granted = false;
                Assert.Throws<InvalidDataException>(() =>
                {
                    if (!fixture.Vector.ContainFlag(queried)) granted = true;
                });
                Assert.False(granted);
            }
            else
            {
                Assert.False(fixture.Vector.TryContainFlag(queried, out bool present));
                Assert.False(present);
            }
            fixture.AssertOnlyStimulusRemains();
            var expected = phase == "membership" ? fixture.Keys.Append(queried) : fixture.Keys;
            Assert.Equal(expected, fixture.Comparer.Reads);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PureFinalRecaptureKeepsExactlyTheExistingComparerReadsForValidMembership(bool physicallyPresent)
    {
        var fixture = new FinalIntegrityFixture(); int queried = physicallyPresent ? fixture.Keys[0] : 18;
        var before = CaptureVector(fixture.Vector); var cache = ReadPrivateCache(fixture.Vector);
        fixture.Comparer.TriggerOnRead = int.MaxValue; fixture.Comparer.Armed = true;
        Assert.True(fixture.Vector.TryContainFlag(queried, out bool present)); Assert.Equal(physicallyPresent, present);
        Assert.Equal(fixture.Keys.Append(queried), fixture.Comparer.Reads);
        Assert.False(fixture.Comparer.ReturnedAfterStimulus);
        AssertVectorUnchanged(fixture.Vector, before); Assert.Same(cache, ReadPrivateCache(fixture.Vector));
    }

    [Theory]
    [InlineData("false-presence")] [InlineData("false-absence")]
    public void MembershipCandidateMustAgreeWithLiteralPhysicalKeysEvenWhenComparerReturnsNormally(string mode)
    {
        var comparer = new FinalIntegrityWrongMembershipComparer(mode);
        var flags = new Flags(64) { ArrayFlags = new ConcurrentDictionary<int, Flags.Flag>(comparer) };
        Assert.True(flags.TryAdd(6, 7, false, 0)); Assert.True(flags.TryAdd(7, 11, false, 0));
        var before = CaptureVector(flags); int queried = mode == "false-presence" ? 18 : 6;
        comparer.Armed = true;
        Assert.False(flags.TryContainFlag(queried, out bool present)); Assert.False(present);
        Assert.Equal(3, comparer.Reads); AssertVectorUnchanged(flags, before);
    }

    private sealed class FinalIntegrityWrongMembershipComparer(string mode) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal int Reads;
        public bool Equals(int left, int right) => left == right ||
            Armed && mode == "false-presence" && (left == 6 && right == 18 || left == 18 && right == 6);
        public int GetHashCode(int value)
        {
            if (Armed && ++Reads == 3 && mode == "false-absence") return 1;
            return 0;
        }
    }
}
