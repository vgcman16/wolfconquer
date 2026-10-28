using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public void PreparedRuneRankDoesNotPublishUntilCommitAndKeepsStableTieOrdering()
    {
        const long third = 1293602;
        var ranking = new LegacyRuneRankingState([RuneRankSeed(RuneRankPlayerA, 10300), RuneRankSeed(RuneRankPlayerB, 10300)]);
        var before = ranking.Snapshot();
        using (var lease = ranking.PrepareUpdate(RuneRankCharacter(third, "Third"), RuneRankItems(16)))
        {
            Assert.True(lease.IsCurrentPlain()); Assert.Equal(before, ranking.Snapshot());
            Assert.Throws<InvalidOperationException>(() => ranking.Update(RuneRankCharacter(RuneRankPlayerA, "reentrant"), RuneRankItems(17)));
            Assert.Throws<InvalidOperationException>(() => ranking.PrepareUpdate(RuneRankCharacter(third, "reentrant"), RuneRankItems(16)));
            Assert.Equal(before, ranking.Snapshot()); lease.Publish(); Assert.False(lease.IsCurrentPlain());
            Assert.Throws<InvalidOperationException>(() => lease.Publish());
        }
        Assert.Equal(new[] { RuneRankPlayerA, RuneRankPlayerB, third }, ranking.Snapshot().Select(row => row.CharacterUid));
        Assert.Equal(new[] { RuneRankPlayerA, RuneRankPlayerB }, before.Select(row => row.CharacterUid));
    }

    [Fact]
    public void AbandonedRuneRankCandidateLeavesOriginalTableUntouched()
    {
        var ranking = new LegacyRuneRankingState([RuneRankSeed(RuneRankPlayerB, 10000)]);
        var before = ranking.Snapshot();
        using (var lease = ranking.PrepareUpdate(RuneRankCharacter(RuneRankPlayerA, "Abandoned"), RuneRankItems(17)))
            Assert.True(lease.IsCurrentPlain());
        Assert.Equal(before, ranking.Snapshot());
        ranking.Update(RuneRankCharacter(RuneRankPlayerA, "Actual"), RuneRankItems(16));
        Assert.Equal(RuneRankPlayerA, ranking.Snapshot()[0].CharacterUid);
    }
    [Fact]
    public async Task CompetingRealUpdateWaitsUntilPreparedLeasePublishes()
    {
        var ranking = new LegacyRuneRankingState([RuneRankSeed(RuneRankPlayerB, 10000)]);
        using var acquired = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var competing = new ManualResetEventSlim();
        var owner = Task.Factory.StartNew(() =>
        {
            using var lease = ranking.PrepareUpdate(RuneRankCharacter(RuneRankPlayerA, "Prepared"), RuneRankItems(16));
            acquired.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); lease.Publish();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task? other = null;
        try
        {
            Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
            other = Task.Factory.StartNew(() =>
            {
                competing.Set(); ranking.Update(RuneRankCharacter(RuneRankPlayerB, "Later"), RuneRankItems(17));
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(competing.Wait(TimeSpan.FromSeconds(5))); Assert.False(other.Wait(TimeSpan.FromMilliseconds(150)));
        }
        finally { release.Set(); }
        await owner.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(other); await other!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { RuneRankPlayerB, RuneRankPlayerA }, ranking.Snapshot().Select(row => row.CharacterUid));
        Assert.Equal("Later", ranking.Snapshot()[0].CharacterName);
    }
}
