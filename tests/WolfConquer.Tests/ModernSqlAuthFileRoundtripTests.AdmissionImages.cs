using System.Text;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlNormalizedAdmissionImagesColdReadIntoRealNativePreparationWithoutPublication() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var text = SqlOrdinaryAdmissionImagePlanTests.NormalizingText();
        var before = SqlOrdinaryAdmissionImagePlanTests.Bundle(text, SqlOrdinaryAdmissionImagePlanTests.AllSidecars());
        var peer = SqlOrdinaryAdmissionImagePlanTests.Bundle(account: 502, uid: 9002, items: SqlWorldStateTestData.SecondItems());
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, before, 0)),
            SqlWorldStateCommitStatus.Committed, 1, before);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, peer, 0)),
            SqlWorldStateCommitStatus.Committed, 1, peer);
        var originalRows = await database.SnapshotWorldBundleRowsAsync();
        var loadedRead = await database.CreateWorldStateRepository().ReadBundleAsync(before.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, loadedRead.Status); Assert.Equal(SqlWorldStateFailure.None, loadedRead.Failure);
        var loaded = Assert.IsType<SqlWorldStateBundleRecord>(loadedRead.Record);
        Assert.Equal(1L, loaded.Revision); AssertBundleImage(before, loaded.Image);
        var peerRead = await database.CreateWorldStateRepository().ReadBundleAsync(peer.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, peerRead.Status); Assert.Equal(SqlWorldStateFailure.None, peerRead.Failure);
        var loadedPeer = Assert.IsType<SqlWorldStateBundleRecord>(peerRead.Record);
        Assert.Equal(1L, loadedPeer.Revision); AssertBundleImage(peer, loadedPeer.Image);
        var plan = SqlOrdinaryAdmissionImagePlanTests.Prepare(loaded.Image, SqlOrdinaryAdmissionImagePlanTests.Deadline.AddTicks(1));
        Assert.Same(loaded.Image, plan.Before);
        Assert.True(plan.HasChanges);
        Assert.Equal(Encoding.UTF8.GetBytes(SqlOrdinaryAdmissionImagePlanTests.ExpectedNormalizedText(text)), plan.After.Core.CopyCharacterBytes());
        Assert.Equal(Convert.FromHexString("02000000EA03020078563412030401A5560400000000000000000000"),
            SqlWorldStateBundleTests.Sidecar(plan.After, SqlWorldStateSidecarKind.Spells).CopyBytes());
        SqlOrdinaryAdmissionImagePlanTests.AssertUnchangedOwnershipAndSidecars(before, plan.After, spellsMayChange: true);
        Assert.Equal(originalRows, await database.SnapshotWorldBundleRowsAsync()); // Preparing DATA performs no SQL write.
        var request = BundleRequest(SqlWorldStateOperationKind.Save, plan.After, loaded.Revision);
        AssertBundleCommit(await repository.SaveBundleAsync(request), SqlWorldStateCommitStatus.Committed, 2, plan.After);
        var coldRead = await database.CreateWorldStateRepository().ReadBundleAsync(501);
        Assert.Equal(SqlWorldStateReadStatus.Found, coldRead.Status); Assert.Equal(SqlWorldStateFailure.None, coldRead.Failure);
        var cold = Assert.IsType<SqlWorldStateBundleRecord>(coldRead.Record);
        Assert.Equal(2L, cold.Revision); AssertBundleImage(plan.After, cold.Image);
        await AssertBundleReadAsync(database, loadedPeer.Image, loadedPeer.Revision);
        var settledRows = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(originalRows.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)),
            settledRows.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)));
        await AssertSqlColdImageNativePreparationOnlyAsync(cold.Image);
        Assert.Equal(settledRows, await database.SnapshotWorldBundleRowsAsync());
        Assert.False(SqlOrdinaryAdmissionImagePlanTests.Prepare(cold.Image, SqlOrdinaryAdmissionImagePlanTests.Deadline.AddTicks(1)).HasChanges);
        AssertBundleCommit(await database.CreateWorldStateRepository().SaveBundleAsync(request), SqlWorldStateCommitStatus.Replayed, 2, plan.After);
        Assert.Equal(settledRows, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlPreparedAdmissionPairSecondUpdateRollsBackAllSixImagesAndUidLedgerThenRetriesSameOperation() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var first = SqlOrdinaryAdmissionImagePlanTests.Bundle(SqlOrdinaryAdmissionImagePlanTests.NormalizingText(),
            SqlOrdinaryAdmissionImagePlanTests.AllSidecars());
        var otherText = SqlOrdinaryAdmissionImagePlanTests.NormalizingText(9002)
            .Replace("\r\nMoney=1000\r\n", "\r\nMoney=2000\r\n", StringComparison.Ordinal)
            .Replace("\r\nConquerPoints=100\r\n", "\r\nConquerPoints=200\r\n", StringComparison.Ordinal);
        var second = SqlOrdinaryAdmissionImagePlanTests.Bundle(otherText, SqlOrdinaryAdmissionImagePlanTests.AllSidecars(),
            account: 502, uid: 9002, items: SqlWorldStateTestData.SecondItems());
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, first, 0)),
            SqlWorldStateCommitStatus.Committed, 1, first);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, second, 0)),
            SqlWorldStateCommitStatus.Committed, 1, second);
        var before = await database.SnapshotWorldBundleRowsAsync();
        var firstRead = await database.CreateWorldStateRepository().ReadBundleAsync(first.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, firstRead.Status); Assert.Equal(SqlWorldStateFailure.None, firstRead.Failure);
        var loadedFirst = Assert.IsType<SqlWorldStateBundleRecord>(firstRead.Record);
        Assert.Equal(1L, loadedFirst.Revision); AssertBundleImage(first, loadedFirst.Image);
        var secondRead = await database.CreateWorldStateRepository().ReadBundleAsync(second.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, secondRead.Status); Assert.Equal(SqlWorldStateFailure.None, secondRead.Failure);
        var loadedSecond = Assert.IsType<SqlWorldStateBundleRecord>(secondRead.Record);
        Assert.Equal(1L, loadedSecond.Revision); AssertBundleImage(second, loadedSecond.Image);
        var at = SqlOrdinaryAdmissionImagePlanTests.Deadline.AddTicks(1);
        var firstPlan = SqlOrdinaryAdmissionImagePlanTests.Prepare(loadedFirst.Image, at);
        var secondPlan = SqlOrdinaryAdmissionImagePlanTests.Prepare(loadedSecond.Image, at);
        Assert.Same(loadedFirst.Image, firstPlan.Before); Assert.Same(loadedSecond.Image, secondPlan.Before);
        Assert.True(firstPlan.HasChanges); Assert.True(secondPlan.HasChanges);
        SqlOrdinaryAdmissionImagePlanTests.AssertUnchangedOwnershipAndSidecars(first, firstPlan.After, spellsMayChange: true);
        SqlOrdinaryAdmissionImagePlanTests.AssertUnchangedOwnershipAndSidecars(second, secondPlan.After, spellsMayChange: true);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        var request = BundlePair(firstPlan.After, secondPlan.After, loadedFirst.Revision, loadedSecond.Revision);
        await database.InstallCompletePairSecondWriteFailureAsync(firstPlan.After, 2, second.AccountId);
        var error = await Assert.ThrowsAsync<MySqlException>(() => repository.SavePairBundleAsync(request).AsTask());
        Assert.Equal(1644, error.Number);
        Assert.Contains("owned-bundle-first-six-and-uids-observed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("owned-bundle-first-six-and-uids-not-observed", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, first, 1); await AssertBundleReadAsync(database, second, 1);
        await database.DropCompletePairSecondWriteFailureAsync();
        AssertBundleCommit(await database.CreateWorldStateRepository().SavePairBundleAsync(request), SqlWorldStateCommitStatus.Committed,
            2, firstPlan.After, secondPlan.After);
        await AssertBundleReadAsync(database, firstPlan.After, 2); await AssertBundleReadAsync(database, secondPlan.After, 2);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(5, settled.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        Assert.Equal(3, settled.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        AssertBundleCommit(await database.CreateWorldStateRepository().SavePairBundleAsync(request), SqlWorldStateCommitStatus.Replayed,
            2, firstPlan.After, secondPlan.After);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
    });
}
