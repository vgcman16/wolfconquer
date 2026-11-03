using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldStaleIdentityAndForeignPhysicalUidRefusalsLeaveEveryRowUnchanged() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository);
        var initial = SqlWorldStateTestData.Initial();
        var before = await database.SnapshotWorldStateRowsAsync();
        await AssertWorldRefusedAsync(database, before,
            repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, initial.First, 0)), SqlWorldStateFailure.StateExists);
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, initial.First, 2)), SqlWorldStateFailure.RevisionConflict);
        await AssertWorldRefusedAsync(database, before,
            repository.SavePairAsync(WorldPair(initial.First, initial.Second, secondRevision: 9)), SqlWorldStateFailure.RevisionConflict);

        // A new account must not create another row for an already-owned character UID.
        var reusedCharacter = SqlWorldStateTestData.Image(503, initial.First.CharacterUid, PlayerItemSnapshot.Empty);
        await AssertWorldRefusedAsync(database, before,
            repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, reusedCharacter, 0)), SqlWorldStateFailure.CharacterIdentityConflict);

        // Individually valid image, but this account cannot replace its persisted character identity.
        var differentCharacter = SqlWorldStateTestData.Image(SqlWorldStateTestData.FirstAccount, 9999, SqlWorldStateTestData.FirstItems());
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, differentCharacter, 1)), SqlWorldStateFailure.CharacterIdentityConflict);
        // The root and retained child are part of the same global physical UID domain.
        var foreignChild = SqlWorldStateTestData.Image(SqlWorldStateTestData.SecondAccount, SqlWorldStateTestData.SecondCharacter,
            SqlWorldStateTestData.SecondItems() with
            { InventoryItems = [SqlWorldStateTestData.Item(SqlWorldStateTestData.ChildUid)] });
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, foreignChild, 1)), SqlWorldStateFailure.ItemUidConflict);
        var foreignRoot = SqlWorldStateTestData.Image(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.FirstCharacter,
            SqlWorldStateTestData.SecondItems());
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, foreignRoot, 1)), SqlWorldStateFailure.ItemUidConflict);
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Create, initial.First, 0)), SqlWorldStateFailure.InvalidOperationKind);
        await AssertWorldReadAsync(database, initial.First, 1); await AssertWorldReadAsync(database, initial.Second, 1);
        Assert.Equal(before, await database.SnapshotWorldStateRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldMissingAndMalformedStoredImagesRemainDistinctWithoutDefaultOrRepair() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository);
        var initial = SqlWorldStateTestData.Initial();
        var before = await database.SnapshotWorldStateRowsAsync();
        var missing = await database.CreateWorldStateRepository().ReadAsync(503);
        Assert.Equal(SqlWorldStateReadStatus.Missing, missing.Status); Assert.Null(missing.Record);
        Assert.Equal(SqlWorldStateFailure.MissingState, missing.Failure);
        await AssertWorldRefusedAsync(database, before,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, SqlWorldStateTestData.Image(503, 9003), 1)),
            SqlWorldStateFailure.MissingState);

        await database.CorruptWorldCharacterImageAsync(initial.First.AccountId);
        var corrupted = await database.SnapshotWorldStateRowsAsync();
        Assert.False(before.SequenceEqual(corrupted));
        var invalid = await database.CreateWorldStateRepository().ReadAsync(initial.First.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, invalid.Status); Assert.Null(invalid.Record);
        Assert.Equal(SqlWorldStateFailure.InvalidStoredState, invalid.Failure);
        await AssertWorldRefusedAsync(database, corrupted,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, initial.First, 1)), SqlWorldStateFailure.InvalidStoredState);
        await AssertWorldReadAsync(database, initial.Second, 1);
        Assert.Equal(corrupted, await database.SnapshotWorldStateRowsAsync());
    });
}
