using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteBundleColdReadSingleSaveAndHistoricalReplayRetainAllSixImages() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var core = SqlWorldStateTestData.Initial(); var first = CompleteWorldImage(core.First); var second = CompleteWorldImage(core.Second);
        Assert.NotEqual(first.AccountId, (long)first.CharacterUid);
        var create = BundleRequest(SqlWorldStateOperationKind.Create, first, 0);
        AssertBundleCommit(await repository.CreateBundleAsync(create), SqlWorldStateCommitStatus.Committed, 1, first);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, second, 0)), SqlWorldStateCommitStatus.Committed, 1, second);
        await AssertBundleReadAsync(database, first, 1); await AssertBundleReadAsync(database, second, 1);
        var before = await database.SnapshotWorldBundleRowsAsync();
        var updated = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1007, 101), 1);
        var save = BundleRequest(SqlWorldStateOperationKind.Save, updated, 1);
        AssertBundleCommit(await repository.SaveBundleAsync(save), SqlWorldStateCommitStatus.Committed, 2, updated);
        await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, second, 1);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(before.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)), settled.Where(row => row.StartsWith("state|502|", StringComparison.Ordinal)));
        Assert.Contains("operation|" + Convert.ToHexString(save.OperationId.ToByteArray()) + "|" + Convert.ToHexString(save.CopyDescriptorHash()) + "|" + Convert.ToHexString(save.CopyDescriptorBytes()), settled);
        AssertBundleCommit(await database.CreateWorldStateRepository().SaveBundleAsync(save), SqlWorldStateCommitStatus.Replayed, 2, updated);
        AssertBundleCommit(await database.CreateWorldStateRepository().CreateBundleAsync(create), SqlWorldStateCommitStatus.Replayed, 1, first);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync()); await AssertBundleReadAsync(database, updated, 2);
        var changed = CompleteWorldImage(updated.Core, 2); // Same core; different retained sidecars must conflict.
        await AssertBundleRefusedAsync(database, settled, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, changed, 1, save.OperationId)), SqlWorldStateFailure.OperationConflict);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteBundleNullAbsenceAndPresentEmptyPersistDifferentFormatTwoStates() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var missing = SqlWorldStateBundleTests.Bundle(SqlWorldStateTestData.Image(501, 9001));
        var present = SqlWorldStateBundleTests.Bundle(SqlWorldStateTestData.Image(502, 9002),
        [
            new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.EmptyGear()),
        ]);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, missing, 0)), SqlWorldStateCommitStatus.Committed, 1, missing);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, present, 0)), SqlWorldStateCommitStatus.Committed, 1, present);
        await AssertBundleReadAsync(database, missing, 1); await AssertBundleReadAsync(database, present, 1);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        var absentRow = Assert.Single(rows, row => row.StartsWith("state|501|9001|1|2|", StringComparison.Ordinal));
        Assert.EndsWith("|<NULL>|<NULL>|<NULL>|<NULL>", absentRow, StringComparison.Ordinal);
        var presentRow = Assert.Single(rows, row => row.StartsWith("state|502|9002|1|2|", StringComparison.Ordinal));
        Assert.EndsWith("|00000000|00000000|00000000|" + Convert.ToHexString(SqlWorldStateBundleTests.EmptyGear()), presentRow, StringComparison.Ordinal);
        var oldRead = await repository.ReadAsync(501);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, oldRead.Status); Assert.Null(oldRead.Record); Assert.Equal(SqlWorldStateFailure.StateFormatConflict, oldRead.Failure);
        Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompletePairMovesRootChildAndCurrencyWithEverySidecarAndColdReplay() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedBundlePairAsync(database, repository);
        var core = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(core.First, 1); var second = CompleteWorldImage(core.Second, 2);
        var request = BundlePair(first, second);
        AssertBundleCommit(await repository.SavePairBundleAsync(request), SqlWorldStateCommitStatus.Committed, 2, first, second);
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        Assert.Contains("uid|71001|502", rows); Assert.Contains("uid|71002|502", rows); Assert.Contains("uid|72001|501", rows);
        Assert.Equal(5, rows.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        var firstCharacter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(first.Core.CopyCharacterBytes(), out _));
        var secondCharacter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(second.Core.CopyCharacterBytes(), out _));
        Assert.Equal((1025L, 103L), (firstCharacter.Money, firstCharacter.ConquerPoints));
        Assert.Equal((1975L, 197L), (secondCharacter.Money, secondCharacter.ConquerPoints));
        AssertBundleCommit(await database.CreateWorldStateRepository().SavePairBundleAsync(request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
    });
}
