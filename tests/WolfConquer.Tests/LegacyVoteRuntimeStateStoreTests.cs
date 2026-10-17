using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteRuntimeStateStoreTests : IDisposable
{
    private static readonly DateTime FirstTimer = new(638379291346047487L, DateTimeKind.Unspecified);
    private static readonly DateTime SecondTimer = new(638379327346047487L, DateTimeKind.Unspecified);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-vote-runtime-" + Guid.NewGuid().ToString("N"));

    public LegacyVoteRuntimeStateStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void LoadsLiveShapeAndRoundTripsThroughPersistencePlan()
    {
        var originalText =
            "Count=2\r\n" +
            $"1296463/198.51.100.15/{FirstTimer.Ticks}/\r\n" +
            $"1296464/203.0.113.7/{SecondTimer.Ticks}/\r\n";
        WriteVotes(originalText);

        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);

        Assert.True(store.LoadResult.Success);
        Assert.True(store.LoadResult.FileExisted);
        Assert.Equal(2, store.LoadResult.RecordCount);
        Assert.Null(store.LoadResult.FailureReason);
        Assert.Equal(
            [
                new LegacyVoteRuntimeRecord(1_296_463, "198.51.100.15", FirstTimer),
                new LegacyVoteRuntimeRecord(1_296_464, "203.0.113.7", SecondTimer)
            ],
            store.Snapshot);

        LegacyVotePersistencePlan? capturedPlan = null;
        var claim = store.TryCommitDueClaim(
            uid: 1_296_463,
            ipAddress: "198.51.100.9",
            now: FirstTimer,
            plan =>
            {
                capturedPlan = plan;
                File.WriteAllBytes(plan.VotesFilePath, plan.VotesFileBytes.ToArray());
                return true;
            });

        Assert.True(claim.Success);
        Assert.Equal(new LegacyVoteRuntimeRecord(1_296_463, "198.51.100.15", FirstTimer), claim.EffectiveRecord);
        Assert.NotNull(capturedPlan);
        Assert.Equal(VotesPath, capturedPlan.VotesFilePath);
        Assert.Equal(Encoding.ASCII.GetBytes(originalText), capturedPlan.VotesFileBytes.ToArray());

        var reloaded = LegacyVoteRuntimeStateStore.Load(VotesPath);
        Assert.True(reloaded.LoadResult.Success);
        Assert.Equal(store.Snapshot, reloaded.Snapshot);
    }

    [Fact]
    public void MissingFileLoadsAsEmptyAndAllowsAnUnmatchedVote()
    {
        var missingPath = Path.Combine(_root, "missing", "Votes.txt");

        var store = LegacyVoteRuntimeStateStore.Load(missingPath);

        Assert.True(store.LoadResult.Success);
        Assert.False(store.LoadResult.FileExisted);
        Assert.Equal(0, store.LoadResult.RecordCount);
        Assert.Null(store.LoadResult.FailureReason);
        Assert.Empty(store.Snapshot);
        Assert.True(store.CanVote(42, "192.0.2.42", FirstTimer));
    }

    [Fact]
    public void LoadsFullUnsignedUidRangeDespiteSignedSnapshotModel()
    {
        WriteVotes($"Count=1\r\n{uint.MaxValue}/192.0.2.42/{FirstTimer.Ticks}/\r\n");

        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);

        Assert.True(store.LoadResult.Success, store.LoadResult.FailureReason);
        Assert.Equal(
            new LegacyVoteRuntimeRecord(uint.MaxValue, "192.0.2.42", FirstTimer),
            Assert.Single(store.Snapshot));
        Assert.False(store.CanVote(uint.MaxValue, "198.51.100.42", FirstTimer.AddHours(1)));
    }

    [Fact]
    public void MalformedExistingFileReportsReasonAndFailsClosed()
    {
        WriteVotes("Count=1\r\n42/192.0.2.42/not-ticks/\r\n");
        var callbackCalled = false;

        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var claim = store.TryCommitDueClaim(
            uid: 42,
            ipAddress: "192.0.2.42",
            now: FirstTimer,
            _ =>
            {
                callbackCalled = true;
                return true;
            });

        Assert.False(store.LoadResult.Success);
        Assert.True(store.LoadResult.FileExisted);
        Assert.Equal(0, store.LoadResult.RecordCount);
        Assert.Contains("DateTime ticks", store.LoadResult.FailureReason, StringComparison.Ordinal);
        Assert.Empty(store.Snapshot);
        Assert.False(store.CanVote(42, "192.0.2.42", FirstTimer.AddYears(1)));
        Assert.False(claim.Success);
        Assert.Contains("unavailable", claim.FailureReason, StringComparison.Ordinal);
        Assert.Null(claim.EffectiveRecord);
        Assert.False(callbackCalled);
    }

    [Fact]
    public void CanVoteUsesUidMatchEvenWhenIpDiffers()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);

        Assert.False(store.CanVote(42, "198.51.100.8", FirstTimer.AddHours(1)));
        Assert.True(store.CanVote(42, "198.51.100.8", FirstTimer.AddHours(13)));
    }

    [Fact]
    public void CanVoteUsesIpMatchEvenWhenUidDiffers()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);

        Assert.False(store.CanVote(99, "192.0.2.42", FirstTimer.AddHours(1)));
        Assert.True(store.CanVote(99, "192.0.2.42", FirstTimer.AddHours(13)));
    }

    [Fact]
    public void CanVoteUsesFirstUidOrIpMatchInListOrder()
    {
        var now = FirstTimer.AddDays(2);
        WriteVotes(
            "Count=2\r\n" +
            $"100/192.0.2.10/{now.AddHours(-13).Ticks}/\r\n" +
            $"200/192.0.2.20/{now.AddHours(-1).Ticks}/\r\n");
        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);

        Assert.True(store.CanVote(200, "192.0.2.10", now));
    }

    [Fact]
    public void CanVoteRequiresNowToBeStrictlyLaterThanTwelveHourBoundary()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);
        var boundary = FirstTimer.AddHours(12);

        Assert.False(store.CanVote(42, "192.0.2.42", boundary));
        Assert.True(store.CanVote(42, "192.0.2.42", boundary.AddTicks(1)));
        Assert.True(store.CanVote(99, "198.51.100.99", boundary));
    }

    [Fact]
    public void CanVoteFailsClosedWhenCooldownAdditionWouldOverflow()
    {
        var store = LoadSingle(42, "192.0.2.42", DateTime.MaxValue);

        Assert.False(store.CanVote(42, "192.0.2.42", DateTime.MaxValue));
    }

    [Fact]
    public void DueClaimUpdatesOnlyTimerAndDoesNotRecheckCooldown()
    {
        var claimTime = FirstTimer.AddMinutes(1);
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);
        LegacyVotePersistencePlan? capturedPlan = null;
        Assert.False(store.CanVote(42, "198.51.100.42", claimTime));

        var result = store.TryCommitDueClaim(
            uid: 42,
            ipAddress: "198.51.100.42",
            now: claimTime,
            plan =>
            {
                capturedPlan = plan;
                return true;
            });

        var expected = new LegacyVoteRuntimeRecord(42, "192.0.2.42", claimTime);
        Assert.True(result.Success);
        Assert.Equal(expected, result.EffectiveRecord);
        Assert.Equal(expected, Assert.Single(store.Snapshot));
        Assert.Contains("42/192.0.2.42/", Encoding.ASCII.GetString(capturedPlan!.VotesFileBytes.Span), StringComparison.Ordinal);
        Assert.DoesNotContain("198.51.100.42", Encoding.ASCII.GetString(capturedPlan.VotesFileBytes.Span), StringComparison.Ordinal);
    }

    [Fact]
    public void DueClaimAppendsWhenNoUidOrIpMatches()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);

        var result = store.TryCommitDueClaim(
            uid: 99,
            ipAddress: "198.51.100.99",
            now: SecondTimer,
            _ => true);

        Assert.True(result.Success);
        Assert.Equal(new LegacyVoteRuntimeRecord(99, "198.51.100.99", SecondTimer), result.EffectiveRecord);
        Assert.Equal(
            [
                new LegacyVoteRuntimeRecord(42, "192.0.2.42", FirstTimer),
                new LegacyVoteRuntimeRecord(99, "198.51.100.99", SecondTimer)
            ],
            store.Snapshot);
    }

    [Fact]
    public void DueClaimUpdatesFirstUidOrIpMatchInListOrder()
    {
        WriteVotes(
            "Count=2\r\n" +
            $"100/192.0.2.10/{FirstTimer.Ticks}/\r\n" +
            $"200/192.0.2.20/{FirstTimer.Ticks}/\r\n");
        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);

        var result = store.TryCommitDueClaim(
            uid: 200,
            ipAddress: "192.0.2.10",
            now: SecondTimer,
            _ => true);

        Assert.True(result.Success);
        Assert.Equal(new LegacyVoteRuntimeRecord(100, "192.0.2.10", SecondTimer), result.EffectiveRecord);
        Assert.Equal(
            [
                new LegacyVoteRuntimeRecord(100, "192.0.2.10", SecondTimer),
                new LegacyVoteRuntimeRecord(200, "192.0.2.20", FirstTimer)
            ],
            store.Snapshot);
    }

    [Fact]
    public void SharedIpSecondClaimUpdatesFirstRecordAndPreservesItsUid()
    {
        var store = LegacyVoteRuntimeStateStore.Load(Path.Combine(_root, "new", "Votes.txt"));
        var firstClaim = store.TryCommitDueClaim(100, "192.0.2.50", FirstTimer, _ => true);

        var secondClaim = store.TryCommitDueClaim(200, "192.0.2.50", SecondTimer, _ => true);

        Assert.True(firstClaim.Success);
        Assert.True(secondClaim.Success);
        var record = Assert.Single(store.Snapshot);
        Assert.Equal(new LegacyVoteRuntimeRecord(100, "192.0.2.50", SecondTimer), record);
        Assert.Equal(record, secondClaim.EffectiveRecord);
    }

    [Fact]
    public void CallbackFailureLeavesMemoryUnchanged()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);
        var before = store.Snapshot;
        LegacyVotePersistencePlan? capturedPlan = null;

        var result = store.TryCommitDueClaim(
            uid: 42,
            ipAddress: "192.0.2.42",
            now: SecondTimer,
            plan =>
            {
                capturedPlan = plan;
                return false;
            });

        Assert.False(result.Success);
        Assert.Null(result.EffectiveRecord);
        Assert.Contains("reported failure", result.FailureReason, StringComparison.Ordinal);
        Assert.Equal(before, store.Snapshot);
        Assert.Contains(SecondTimer.Ticks.ToString(), Encoding.ASCII.GetString(capturedPlan!.VotesFileBytes.Span), StringComparison.Ordinal);
    }

    [Fact]
    public void CallbackThrowReturnsFailureAndLeavesMemoryUnchanged()
    {
        var store = LoadSingle(42, "192.0.2.42", FirstTimer);
        var before = store.Snapshot;

        var result = store.TryCommitDueClaim(
            uid: 42,
            ipAddress: "192.0.2.42",
            now: SecondTimer,
            _ => throw new InvalidOperationException("transaction failed"));

        Assert.False(result.Success);
        Assert.Null(result.EffectiveRecord);
        Assert.Contains("InvalidOperationException", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("transaction failed", result.FailureReason, StringComparison.Ordinal);
        Assert.Equal(before, store.Snapshot);
    }

    [Fact]
    public void PersistenceBytesAreDeterministicAsciiWithoutBomAndPreserveListOrder()
    {
        WriteVotes($"Count=1\n7/203.0.113.7/{FirstTimer.Ticks}/\n");
        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);
        LegacyVotePersistencePlan? capturedPlan = null;

        var result = store.TryCommitDueClaim(
            uid: 8,
            ipAddress: "2001:db8::8",
            now: SecondTimer,
            plan =>
            {
                capturedPlan = plan;
                return true;
            });

        var expectedText =
            "Count=2\r\n" +
            $"7/203.0.113.7/{FirstTimer.Ticks}/\r\n" +
            $"8/2001:db8::8/{SecondTimer.Ticks}/\r\n";
        var expectedBytes = Encoding.ASCII.GetBytes(expectedText);

        Assert.True(result.Success);
        Assert.NotNull(capturedPlan);
        Assert.Equal(expectedBytes, capturedPlan.VotesFileBytes.ToArray());
        Assert.False(capturedPlan.VotesFileBytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.All(capturedPlan.VotesFileBytes.ToArray(), value => Assert.InRange(value, (byte)0, (byte)0x7F));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string VotesPath => Path.Combine(_root, "Votes.txt");

    private LegacyVoteRuntimeStateStore LoadSingle(uint uid, string ipAddress, DateTime timer)
    {
        WriteVotes($"Count=1\r\n{uid}/{ipAddress}/{timer.Ticks}/\r\n");
        var store = LegacyVoteRuntimeStateStore.Load(VotesPath);
        Assert.True(store.LoadResult.Success, store.LoadResult.FailureReason);
        return store;
    }

    private void WriteVotes(string text)
    {
        File.WriteAllBytes(VotesPath, Encoding.ASCII.GetBytes(text));
    }
}
