using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualPairCommitAckHeldWhilePeerDiesRetainsKnownOutcomeOnBothOwners() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(exchanged.First, 1); var second = CompleteWorldImage(exchanged.Second, 2);
        var request = BundlePair(first, second); var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        await using (var proxy = database.CreateCommitAckGateProxy())
        {
            var a = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.First.AccountId);
            Assert.True(a.IsAcquired); await using var owner1 = Assert.IsType<SqlWorldBundleCustody>(a.Custody);
            var b = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
            Assert.True(b.IsAcquired); await using var owner2 = Assert.IsType<SqlWorldBundleCustody>(b.Custody);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var operation = owner2.SavePairBundleAsync(owner1, request, deadline.Token).AsTask();
            Exception? error = null;
            try
            {
                var held = await proxy.WaitForHeldCommitAckAsync().WaitAsync(deadline.Token);
                Assert.Equal(2, held.ObservedCommitQueries); Assert.Equal((byte)1, held.AckSequence); Assert.True(held.AckPayloadBytes >= 7);
                Assert.Equal(0, held.StatusFlags & (0x0001 | 0x0008)); Assert.False(operation.IsCompleted);
                await database.KillOwnedWorldCustodyConnectionAsync(initial.Second.AccountId);
            }
            finally
            {
                proxy.ReleaseHeldCommitAck();
                error = await Record.ExceptionAsync(async () => { await operation; });
            }
            Assert.NotNull(error);
            var known = RequirePairException<SqlWorldCustodyOutcomeException>(error);
            Assert.Same(request, known.Request); Assert.NotNull(known.InnerException);
            AssertBundleCommit(known.Result, SqlWorldStateCommitStatus.Committed, 2, first, second);
            var firstAgain = await Record.ExceptionAsync(async () => { await owner1.ReadBundleAsync(); });
            var secondAgain = await Record.ExceptionAsync(async () => { await owner2.ReadBundleAsync(); });
            var pairAgain = await Record.ExceptionAsync(async () => { await owner2.SavePairBundleAsync(owner1, request); });
            Assert.Same(error, firstAgain); Assert.Same(error, secondAgain); Assert.Same(error, pairAgain);
        }
        var settled = await AssertCommittedPairRowsAsync(database, request, first, second);
        await using var reopened1 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), first.AccountId);
        await using var reopened2 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), second.AccountId);
        AssertBundleCommit(await reopened2.SavePairBundleAsync(reopened1, request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
    });
}
