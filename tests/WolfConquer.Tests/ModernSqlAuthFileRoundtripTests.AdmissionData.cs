using System.Text;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteAdmissionDataColdReadHydratesAllSixAndPreparesNativeWithoutPublication() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var original = SqlOrdinaryAdmissionDataTests.CompleteBundle();
        var peer = SqlWorldStateBundleTests.Bundle(SqlWorldStateTestData.Image(502, 9002, SqlWorldStateTestData.SecondItems()));
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, original, 0)),
            SqlWorldStateCommitStatus.Committed, 1, original);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, peer, 0)),
            SqlWorldStateCommitStatus.Committed, 1, peer);
        var before = await database.SnapshotWorldBundleRowsAsync();
        var sourceRead = await database.CreateWorldStateRepository().ReadBundleAsync(original.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, sourceRead.Status); Assert.Equal(SqlWorldStateFailure.None, sourceRead.Failure);
        var source = Assert.IsType<SqlWorldStateBundleRecord>(sourceRead.Record);
        Assert.Equal(1L, source.Revision); AssertBundleImage(original, source.Image);
        var plan = SqlOrdinaryAdmissionDataTests.Plan(source.Image);
        Assert.Same(source.Image, plan.Before); Assert.True(plan.HasChanges);
        var preparedData = plan.ReadPreparedData(); Assert.Same(plan.After, preparedData.Source);
        SqlOrdinaryAdmissionDataTests.AssertCompleteData(preparedData);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        Assert.Equal(Encoding.UTF8.GetBytes(SqlOrdinaryAdmissionDataTests.CharacterText().Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)),
            preparedData.Source.Core.CopyCharacterBytes());
        Assert.Equal(original.Core.CopyItemBytes(), preparedData.Source.Core.CopyItemBytes());
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(original.Sidecars[index].Presence, preparedData.Source.Sidecars[index].Presence);
            Assert.Equal(original.Sidecars[index].CopyBytes(), preparedData.Source.Sidecars[index].CopyBytes());
        }
        SqlOrdinaryAdmissionDataTests.MutateCallerData(preparedData);
        SqlOrdinaryAdmissionDataTests.AssertCompleteData(plan.ReadPreparedData());
        var save = BundleRequest(SqlWorldStateOperationKind.Save, plan.After, source.Revision);
        AssertBundleCommit(await repository.SaveBundleAsync(save), SqlWorldStateCommitStatus.Committed, 2, plan.After);
        var coldRead = await database.CreateWorldStateRepository().ReadBundleAsync(original.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, coldRead.Status); Assert.Equal(SqlWorldStateFailure.None, coldRead.Failure);
        var cold = Assert.IsType<SqlWorldStateBundleRecord>(coldRead.Record);
        Assert.Equal(2L, cold.Revision); AssertBundleImage(plan.After, cold.Image);
        var coldPlan = SqlOrdinaryAdmissionDataTests.Plan(cold.Image);
        Assert.Same(cold.Image, coldPlan.Before); Assert.False(coldPlan.HasChanges);
        Assert.Same(coldPlan.After, coldPlan.ReadPreparedData().Source);
        SqlOrdinaryAdmissionDataTests.AssertCompleteData(coldPlan.ReadPreparedData());
        await SqlOrdinaryAdmissionDataTests.AssertCompleteNativePlanAsync(coldPlan);
        await AssertBundleReadAsync(database, peer, 1);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(before.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)), settled.Where(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        Assert.Equal(before.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)), settled.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)));
        Assert.Equal(16, settled.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        AssertBundleCommit(await database.CreateWorldStateRepository().SaveBundleAsync(save), SqlWorldStateCommitStatus.Replayed, 2, plan.After);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, cold.Image, cold.Revision);
    });
}
