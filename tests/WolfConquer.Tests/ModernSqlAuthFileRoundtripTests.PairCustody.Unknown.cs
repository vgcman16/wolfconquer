using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualPairCustodiesRetainSameUnknownAfterRealCommitAckLoss() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(exchanged.First, 1); var second = CompleteWorldImage(exchanged.Second, 2);
        var request = BundlePair(first, second); var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        await using (var proxy = database.CreateCommitAckDropProxy(skipCommitCount: 1))
        {
            var a = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.First.AccountId);
            Assert.True(a.IsAcquired); await using var owner1 = Assert.IsType<SqlWorldBundleCustody>(a.Custody);
            var b = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
            Assert.True(b.IsAcquired); await using var owner2 = Assert.IsType<SqlWorldBundleCustody>(b.Custody);
            var error = await Record.ExceptionAsync(async () => { await owner2.SavePairBundleAsync(owner1, request); });
            Assert.NotNull(error); RequirePairException<SqlWorldCommitOutcomeUnknownException>(error);
            var observed = await proxy.WaitForCommitAckDropAsync();
            Assert.Equal(2, observed.ObservedCommitQueries); Assert.Equal(1, observed.DroppedServerOkPackets);
            Assert.Equal((byte)1, observed.AckSequence); Assert.True(observed.AckPayloadBytes >= 7);
            Assert.Equal(0, observed.StatusFlags & (0x0001 | 0x0008)); Assert.True(observed.ClientSocketClosed);
            var firstAgain = await Record.ExceptionAsync(async () => { await owner1.ReadBundleAsync(); });
            var secondAgain = await Record.ExceptionAsync(async () => { await owner2.SavePairBundleAsync(owner1, request); });
            Assert.Same(error, firstAgain); Assert.Same(error, secondAgain);
        }
        var settled = await AssertCommittedPairRowsAsync(database, request, first, second);
        await using var reopened1 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), first.AccountId);
        await using var reopened2 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), second.AccountId);
        AssertBundleCommit(await reopened1.SavePairBundleAsync(reopened2, request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
    });

    private static T RequirePairException<T>(Exception error) where T : Exception
    {
        if (error is AggregateException aggregate) return Assert.IsType<T>(Assert.Single(aggregate.Flatten().InnerExceptions, item => item is T));
        return Assert.IsType<T>(error);
    }

    private static async Task<string[]> AssertCommittedPairRowsAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateBundleRequest request,
        SqlWorldStateBundle first, SqlWorldStateBundle second)
    {
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(new[] { "uid|71001|502", "uid|71002|502", "uid|71003|501", "uid|72001|501", "uid|72002|502" },
            rows.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        string operation = "operation|" + Convert.ToHexString(request.OperationId.ToByteArray()) + "|";
        Assert.Equal(operation + Convert.ToHexString(request.CopyDescriptorHash()) + "|" + Convert.ToHexString(request.CopyDescriptorBytes()),
            Assert.Single(rows, row => row.StartsWith(operation, StringComparison.Ordinal)));
        Assert.Equal(3, rows.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        return rows;
    }
}
