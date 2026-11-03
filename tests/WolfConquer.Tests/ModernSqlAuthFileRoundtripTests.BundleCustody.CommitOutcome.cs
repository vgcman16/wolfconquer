using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualBundleCustodyLostSaveCommitAckRetainsUncertaintyAndColdReplay() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var updated = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1007, 101), 1);
        var request = BundleRequest(SqlWorldStateOperationKind.Save, updated, 1);
        var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        var before = await database.SnapshotWorldBundleRowsAsync();
        await using (var proxy = database.CreateCommitAckDropProxy(skipCommitCount: 1))
        {
            var result = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.First.AccountId);
            Assert.True(result.IsAcquired); await using var owner = Assert.IsType<SqlWorldBundleCustody>(result.Custody);
            var unknown = await Record.ExceptionAsync(async () => { await owner.SaveBundleAsync(request); });
            Assert.NotNull(unknown);
            if (unknown is AggregateException aggregate)
                Assert.Contains(aggregate.Flatten().InnerExceptions, error => error is SqlWorldCommitOutcomeUnknownException);
            else Assert.IsType<SqlWorldCommitOutcomeUnknownException>(unknown);
            var observed = await proxy.WaitForCommitAckDropAsync();
            Assert.Equal(2, observed.ObservedCommitQueries); Assert.Equal(1, observed.DroppedServerOkPackets);
            Assert.Equal((byte)1, observed.AckSequence); Assert.True(observed.AckPayloadBytes >= 7);
            Assert.Equal(0, observed.StatusFlags & (0x0001 | 0x0008)); Assert.True(observed.ClientSocketClosed);
            var again = await Record.ExceptionAsync(async () => { await owner.SaveBundleAsync(request); });
            var read = await Record.ExceptionAsync(async () => { await owner.ReadBundleAsync(); });
            Assert.Same(unknown, again); Assert.Same(unknown, read);
        }
        var settled = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(before.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)),
            settled.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)));
        Assert.Equal(before.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)),
            settled.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        string operation = "operation|" + Convert.ToHexString(request.OperationId.ToByteArray()) + "|";
        Assert.Equal(operation + Convert.ToHexString(digest) + "|" + Convert.ToHexString(descriptor),
            Assert.Single(settled, row => row.StartsWith(operation, StringComparison.Ordinal)));
        Assert.Equal(3, settled.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, initial.Second, 1);
        await using var current = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), updated.AccountId);
        AssertBundleCommit(await current.SaveBundleAsync(request), SqlWorldStateCommitStatus.Replayed, 2, updated);
        Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, initial.Second, 1);
    });
}
