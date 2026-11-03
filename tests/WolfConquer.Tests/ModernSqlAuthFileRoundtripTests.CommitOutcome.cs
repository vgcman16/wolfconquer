using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlLostCommitAcknowledgementLeavesCommittedSixImagesAndSameOperationReplaysOnce() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var before = await database.SnapshotWorldBundleRowsAsync();
        var items = SqlWorldStateTestData.FirstItems();
        var updated = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, items with
        { InventoryItems = items.InventoryItems.Append(SqlWorldStateTestData.Item(73001)).ToArray() }, 1007, 101), 1);
        Assert.False(initial.First.Core.CopyCharacterBytes().AsSpan().SequenceEqual(updated.Core.CopyCharacterBytes()));
        Assert.False(initial.First.Core.CopyItemBytes().AsSpan().SequenceEqual(updated.Core.CopyItemBytes()));
        for (int index = 0; index < 4; index++)
            Assert.False(initial.First.Sidecars[index].CopyBytes().AsSpan().SequenceEqual(updated.Sidecars[index].CopyBytes()));
        var request = BundleRequest(SqlWorldStateOperationKind.Save, updated, 1);
        var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        await AssertActualCommitAckLossAsync(database, request);

        // New direct repository/connection bypasses the now-closed one-shot proxy.
        await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, initial.Second, 1);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(before.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)),
            settled.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)));
        Assert.Equal(new[] { "uid|71001|501", "uid|71002|501", "uid|71003|501", "uid|72001|502", "uid|72002|502", "uid|73001|501" },
            settled.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        string operation = "operation|" + Convert.ToHexString(request.OperationId.ToByteArray()) + "|";
        Assert.Equal(operation + Convert.ToHexString(digest) + "|" + Convert.ToHexString(descriptor),
            Assert.Single(settled, row => row.StartsWith(operation, StringComparison.Ordinal)));
        Assert.Equal(3, settled.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        AssertBundleCommit(await database.CreateWorldStateRepository().SaveBundleAsync(request), SqlWorldStateCommitStatus.Replayed, 2, updated);
        Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, initial.Second, 1);
    });

    private static async Task AssertActualCommitAckLossAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateBundleRequest request)
    {
        OwnedSqlCommitAckDropProxy? proxy = null;
        Exception? primary = null, cleanup = null;
        try
        {
            var owned = database.CreateCommitAckDropProxy(); proxy = owned;
            var unknown = await Assert.ThrowsAsync<SqlWorldCommitOutcomeUnknownException>(
                async () => { await owned.CreateWorldStateRepository().SaveBundleAsync(request); });
            Assert.NotNull(unknown.InnerException);
            var observed = await owned.WaitForCommitAckDropAsync();
            Assert.Equal(1, observed.ObservedCommitQueries); Assert.Equal(1, observed.DroppedServerOkPackets);
            Assert.Equal((byte)1, observed.AckSequence); Assert.True(observed.AckPayloadBytes >= 7);
            Assert.Equal(0, observed.StatusFlags & (0x0001 | 0x0008)); Assert.True(observed.ClientSocketClosed);
        }
        catch (Exception error) { primary = error; }
        if (proxy is not null)
            try { await proxy.DisposeAsync(); } catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }
}
