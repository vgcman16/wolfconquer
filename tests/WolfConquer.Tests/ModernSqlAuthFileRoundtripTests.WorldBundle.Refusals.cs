using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteFormatBoundariesAndCrossVersionOperationReuseNeverProjectOrOverwrite() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var core = SqlWorldStateTestData.Initial(); var first = CompleteWorldImage(core.First); var second = CompleteWorldImage(core.Second);
        var oldCreate = WorldRequest(SqlWorldStateOperationKind.Create, core.First, 0);
        var completeCreate = BundleRequest(SqlWorldStateOperationKind.Create, second, 0);
        AssertWorldCommit(await repository.CreateAsync(oldCreate), SqlWorldStateCommitStatus.Committed, 1, core.First);
        AssertBundleCommit(await repository.CreateBundleAsync(completeCreate), SqlWorldStateCommitStatus.Committed, 1, second);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        Assert.EndsWith("|<NULL>|<NULL>|<NULL>|<NULL>", Assert.Single(rows, row => row.StartsWith("state|501|9001|1|1|", StringComparison.Ordinal)), StringComparison.Ordinal);
        var oldRead = await repository.ReadAsync(502); var completeRead = await repository.ReadBundleAsync(501);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, oldRead.Status); Assert.Null(oldRead.Record); Assert.Equal(SqlWorldStateFailure.StateFormatConflict, oldRead.Failure);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, completeRead.Status); Assert.Null(completeRead.Record); Assert.Equal(SqlWorldStateFailure.StateFormatConflict, completeRead.Failure);
        await AssertBundleRefusedAsync(database, rows, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, first, 1)), SqlWorldStateFailure.StateFormatConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, rows, repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, core.Second, 1)), SqlWorldStateFailure.StateFormatConflict);
        await AssertBundleRefusedAsync(database, rows, repository.SavePairBundleAsync(BundlePair(first, second)), SqlWorldStateFailure.StateFormatConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, rows, repository.SavePairAsync(WorldPair(core.First, core.Second)), SqlWorldStateFailure.StateFormatConflict);
        await AssertBundleRefusedAsync(database, rows, repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, first, 0, oldCreate.OperationId)), SqlWorldStateFailure.OperationConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, rows, repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, core.Second, 0, completeCreate.OperationId)), SqlWorldStateFailure.OperationConflict);
        AssertWorldCommit(await repository.CreateAsync(oldCreate), SqlWorldStateCommitStatus.Replayed, 1, core.First);
        AssertBundleCommit(await repository.CreateBundleAsync(completeCreate), SqlWorldStateCommitStatus.Replayed, 1, second);
        Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, core.First.ItemUids.ToArray());
        await database.AddOwnedWorldUidIndexFaultAsync(79998, 501);
        var extraUid = await database.SnapshotWorldBundleRowsAsync(); Assert.Contains("uid|79998|501", extraUid);
        var invalidCore = await repository.ReadAsync(501);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, invalidCore.Status); Assert.Null(invalidCore.Record); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, invalidCore.Failure);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, extraUid, repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, core.First, 1)), SqlWorldStateFailure.InvalidStoredState);
        await AssertBundleReadAsync(database, second, 1); Assert.Equal(extraUid, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteStaleIdentityForeignChildAndWrongMethodRefusalsRetainAllRows() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository); var before = await database.SnapshotWorldBundleRowsAsync();
        await AssertBundleRefusedAsync(database, before, repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, initial.First, 0)), SqlWorldStateFailure.StateExists);
        await AssertBundleRefusedAsync(database, before, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, initial.First, 2)), SqlWorldStateFailure.RevisionConflict);
        await AssertBundleRefusedAsync(database, before, repository.SavePairBundleAsync(BundlePair(initial.First, initial.Second, secondRevision: 9)), SqlWorldStateFailure.RevisionConflict);
        var reusedCharacter = CompleteWorldImage(SqlWorldStateTestData.Image(503, 9001));
        await AssertBundleRefusedAsync(database, before, repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, reusedCharacter, 0)), SqlWorldStateFailure.CharacterIdentityConflict);
        var rebind = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9003, SqlWorldStateTestData.FirstItems()));
        await AssertBundleRefusedAsync(database, before, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, rebind, 1)), SqlWorldStateFailure.CharacterIdentityConflict);
        var foreignChild = CompleteWorldImage(SqlWorldStateTestData.Image(502, 9002, PlayerItemSnapshot.Empty with
        { InventoryItems = [SqlWorldStateTestData.Item(71002)] }));
        await AssertBundleRefusedAsync(database, before, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, foreignChild, 1)), SqlWorldStateFailure.ItemUidConflict);
        await AssertBundleRefusedAsync(database, before, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, initial.First, 0)), SqlWorldStateFailure.InvalidOperationKind);
        var absent = CompleteWorldImage(SqlWorldStateTestData.Image(503, 9003));
        var read = await repository.ReadBundleAsync(503);
        Assert.Equal(SqlWorldStateReadStatus.Missing, read.Status); Assert.Equal(SqlWorldStateFailure.MissingState, read.Failure); Assert.Null(read.Record);
        await AssertBundleRefusedAsync(database, before, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, absent, 1)), SqlWorldStateFailure.MissingState);
        await AssertBundleReadAsync(database, initial.First, 1); await AssertBundleReadAsync(database, initial.Second, 1);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    });
}
