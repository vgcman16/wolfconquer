using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualPairCustodyDisposalWaitsForSameCommitAndColdReplayPreservesExactState() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged();
        var first = CompleteWorldImage(exchanged.First, 1);
        var second = CompleteWorldImage(exchanged.Second, 2);
        var request = BundlePair(first, second);
        var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        OwnedSqlCommitAckDropProxy? proxy = null;
        SqlWorldBundleCustody? owner1 = null, owner2 = null;
        Task<SqlWorldStateBundleCommitResult>? pair = null;
        Task? dispose1 = null, dispose2 = null;
        SqlWorldStateBundleCommitResult? result = null;
        CommitAckGateObservation? forwarded = null;
        var failures = new List<Exception>();
        try
        {
            proxy = database.CreateCommitRequestGateProxy();
            var acquired1 = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.First.AccountId, deadline.Token);
            owner1 = acquired1.Custody;
            Assert.True(acquired1.IsAcquired); Assert.Equal(SqlWorldStateFailure.None, acquired1.Failure);
            var firstOwner = Assert.IsType<SqlWorldBundleCustody>(owner1);
            var acquired2 = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId, deadline.Token);
            owner2 = acquired2.Custody;
            Assert.True(acquired2.IsAcquired); Assert.Equal(SqlWorldStateFailure.None, acquired2.Failure);
            var secondOwner = Assert.IsType<SqlWorldBundleCustody>(owner2);
            pair = secondOwner.SavePairBundleAsync(firstOwner, request, deadline.Token).AsTask();

            var held = await proxy.WaitForHeldCommitRequestAsync().WaitAsync(deadline.Token);
            Assert.Equal(2, held.ObservedCommitQueries);
            Assert.Equal((byte)0, held.Sequence); Assert.Equal(7, held.PayloadBytes);
            Assert.False(pair.IsCompleted);
            // Each async call reaches its real owner gate before returning its
            // pending task. The observed COMMIT request is the ordering barrier.
            dispose1 = firstOwner.DisposeAsync().AsTask();
            dispose2 = secondOwner.DisposeAsync().AsTask();
            Assert.False(dispose1.IsCompleted); Assert.False(dispose2.IsCompleted);
            Assert.False(pair.IsCompleted);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            if (proxy is not null)
                try { proxy.ReleaseHeldCommitRequest(); } catch (Exception cleanup) { failures.Add(cleanup); }
            if (failures.Count != 0)
                try { await deadline.CancelAsync(); } catch (Exception cleanup) { failures.Add(cleanup); }
            // Observation cancellation is not task settlement. Always await the
            // original started operation, then both actual disposal tasks.
            if (pair is not null)
                try { result = await pair; } catch (Exception error) { failures.Add(error); }
            if (proxy is not null && result is not null)
                try { forwarded = await proxy.WaitForForwardedCommitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { failures.Add(error); }
            if (owner1 is not null && dispose1 is null)
                try { dispose1 = owner1.DisposeAsync().AsTask(); } catch (Exception cleanup) { failures.Add(cleanup); }
            if (owner2 is not null && dispose2 is null)
                try { dispose2 = owner2.DisposeAsync().AsTask(); } catch (Exception cleanup) { failures.Add(cleanup); }
            if (dispose1 is not null)
                try { await dispose1; } catch (Exception cleanup) { failures.Add(cleanup); }
            if (dispose2 is not null)
                try { await dispose2; } catch (Exception cleanup) { failures.Add(cleanup); }
            if (proxy is not null)
                try { await proxy.DisposeAsync(); } catch (Exception cleanup) { failures.Add(cleanup); }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);

        AssertBundleCommit(Assert.IsType<SqlWorldStateBundleCommitResult>(result), SqlWorldStateCommitStatus.Committed, 2, first, second);
        var actualAck = Assert.IsType<CommitAckGateObservation>(forwarded);
        Assert.Equal(2, actualAck.ObservedCommitQueries); Assert.Equal((byte)1, actualAck.AckSequence);
        Assert.True(actualAck.AckPayloadBytes >= 7); Assert.Equal(0, actualAck.StatusFlags & (0x0001 | 0x0008));
        var settled = await AssertCommittedPairRowsAsync(database, request, first, second);
        var disposedFirst = Assert.IsType<SqlWorldBundleCustody>(owner1);
        var disposedSecond = Assert.IsType<SqlWorldBundleCustody>(owner2);
        using var after = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        foreach (var owner in new[] { disposedFirst, disposedSecond })
        {
            var retired = await owner.ReadBundleAsync(after.Token);
            Assert.Equal(SqlWorldStateReadStatus.Invalid, retired.Status);
            Assert.Equal(SqlWorldStateFailure.SessionCustodyRetired, retired.Failure); Assert.Null(retired.Record);
        }
        await AssertBundleRefusedAsync(database, settled,
            disposedSecond.SavePairBundleAsync(disposedFirst, request, after.Token), SqlWorldStateFailure.SessionCustodyRetired);
        SqlWorldBundleCustody? reopened1 = null, reopened2 = null;
        try
        {
            reopened1 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), first.AccountId);
            reopened2 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), second.AccountId);
            AssertBundleCommit(await reopened2.SavePairBundleAsync(reopened1, request, after.Token), SqlWorldStateCommitStatus.Replayed, 2, first, second);
            Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
            Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            if (reopened2 is not null)
                try { await reopened2.DisposeAsync(); } catch (Exception cleanup) { failures.Add(cleanup); }
            if (reopened1 is not null)
                try { await reopened1.DisposeAsync(); } catch (Exception cleanup) { failures.Add(cleanup); }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    });
}
