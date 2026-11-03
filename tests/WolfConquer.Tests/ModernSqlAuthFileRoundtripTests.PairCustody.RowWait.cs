using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualNewCustodyWaitsForOlderPairRowBeforeReceivingCommittedAfterimage() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(exchanged.First, 1); var second = CompleteWorldImage(exchanged.Second, 2);
        var request = BundlePair(first, second); var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        OwnedSqlCommitAckDropProxy? proxy = null;
        SqlWorldBundleCustody? owner1 = null, owner2 = null, claimant = null, reopened1 = null;
        Task<SqlWorldStateBundleCommitResult>? pair = null; Task<SqlWorldBundleCustody>? pending = null;
        bool pairObserved = false, claimantObserved = false;
        Exception? primary = null; var cleanup = new List<Exception>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            proxy = database.CreateCommitRequestGateProxy();
            var a = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.First.AccountId);
            owner1 = a.Custody; Assert.True(a.IsAcquired); var firstOwner = Assert.IsType<SqlWorldBundleCustody>(owner1);
            var b = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
            owner2 = b.Custody; Assert.True(b.IsAcquired); var secondOwner = Assert.IsType<SqlWorldBundleCustody>(owner2);
            var operation = secondOwner.SavePairBundleAsync(firstOwner, request, deadline.Token).AsTask(); pair = operation;
            var held = await proxy.WaitForHeldCommitRequestAsync().WaitAsync(deadline.Token);
            Assert.Equal(2, held.ObservedCommitQueries); Assert.Equal((byte)0, held.Sequence); Assert.Equal(7, held.PayloadBytes);
            Assert.False(operation.IsCompleted);
            await database.KillOwnedWorldCustodyConnectionAsync(initial.Second.AccountId);
            var claimantTask = AcquireAfterRealCloseAsync(database.CreateWorldStateRepository(), initial.Second.AccountId, deadline.Token); pending = claimantTask;
            await database.WaitForOwnedPairRowWaitAsync(initial.First.AccountId, initial.Second.AccountId, deadline.Token);
            Assert.False(claimantTask.IsCompleted); Assert.False(operation.IsCompleted);
            proxy.ReleaseHeldCommitRequest();
            var pairError = await Record.ExceptionAsync(async () => { await operation; }); pairObserved = true;
            try { claimant = await claimantTask; } finally { claimantObserved = true; }
            var newOwner = Assert.IsType<SqlWorldBundleCustody>(claimant);
            Assert.NotNull(pairError);
            var known = RequirePairException<SqlWorldCustodyOutcomeException>(pairError);
            Assert.Same(request, known.Request); Assert.NotNull(known.InnerException);
            AssertBundleCommit(known.Result, SqlWorldStateCommitStatus.Committed, 2, first, second);
            var oldFirst = await Record.ExceptionAsync(async () => { await firstOwner.ReadBundleAsync(); });
            var oldSecond = await Record.ExceptionAsync(async () => { await secondOwner.ReadBundleAsync(); });
            Assert.Same(pairError, oldFirst); Assert.Same(pairError, oldSecond);
            var forwarded = await proxy.WaitForForwardedCommitAsync().WaitAsync(deadline.Token);
            Assert.Equal(2, forwarded.ObservedCommitQueries); Assert.Equal((byte)1, forwarded.AckSequence);
            Assert.True(forwarded.AckPayloadBytes >= 7); Assert.Equal(0, forwarded.StatusFlags & (0x0001 | 0x0008));
            var newRead = await newOwner.ReadBundleAsync(); Assert.Equal(SqlWorldStateReadStatus.Found, newRead.Status);
            var current = Assert.IsType<SqlWorldStateBundleRecord>(newRead.Record); Assert.Equal(2, current.Revision); AssertBundleImage(second, current.Image);
            var settled = await AssertCommittedPairRowsAsync(database, request, first, second);
            reopened1 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), first.AccountId);
            AssertBundleCommit(await reopened1.SavePairBundleAsync(newOwner, request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
            Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
            Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (proxy is not null) try { proxy.ReleaseHeldCommitRequest(); } catch (Exception error) { cleanup.Add(error); }
            if (primary is not null) try { deadline.Cancel(); } catch (Exception error) { cleanup.Add(error); }
            if (pair is not null && !pairObserved)
                try { await pair; } catch (Exception error) { cleanup.Add(error); }
            if (pending is not null && !claimantObserved)
                try { claimant = await pending; } catch (Exception error) { cleanup.Add(error); }
            if (reopened1 is not null) try { await reopened1.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
            if (claimant is not null) try { await claimant.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
            if (owner2 is not null) try { await owner2.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
            if (owner1 is not null) try { await owner1.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
            if (proxy is not null) try { await proxy.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException(cleanup);
    });

    private static async Task<SqlWorldBundleCustody> AcquireAfterRealCloseAsync(SqlWorldStateRepository repository, long accountId, CancellationToken token)
    {
        while (true)
        {
            var result = await repository.TryAcquireBundleCustodyAsync(accountId, token);
            if (result.IsAcquired)
            {
                Assert.Equal(SqlWorldStateFailure.None, result.Failure);
                return Assert.IsType<SqlWorldBundleCustody>(result.Custody);
            }
            Assert.Null(result.Custody); Assert.Equal(SqlWorldStateFailure.SessionCustodyConflict, result.Failure);
            await Task.Delay(TimeSpan.FromMilliseconds(25), token);
        }
    }
}
