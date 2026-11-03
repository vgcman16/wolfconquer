using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteMalformedStoredKindsPresenceAndFormatRemainInvalidWithoutRepair() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository); var baseline = await database.SnapshotWorldBundleRowsAsync();
        foreach (var fault in new[] { OwnedBundleStoredFault.Character, OwnedBundleStoredFault.Items, OwnedBundleStoredFault.Spells,
            OwnedBundleStoredFault.SpellMembership, OwnedBundleStoredFault.Proficiencies, OwnedBundleStoredFault.ProficiencyMembership,
            OwnedBundleStoredFault.Quests, OwnedBundleStoredFault.CombatGear, OwnedBundleStoredFault.EmptyPresent,
            OwnedBundleStoredFault.UnknownFormat, OwnedBundleStoredFault.CoreFormatWithSidecar })
        {
            await database.ApplyOwnedBundleStoredFaultAsync(501, fault);
            var corrupt = await database.SnapshotWorldBundleRowsAsync(); Assert.False(baseline.SequenceEqual(corrupt));
            var read = await database.CreateWorldStateRepository().ReadBundleAsync(501);
            Assert.Equal(SqlWorldStateReadStatus.Invalid, read.Status); Assert.Null(read.Record); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, read.Failure);
            if (fault is OwnedBundleStoredFault.UnknownFormat or OwnedBundleStoredFault.CoreFormatWithSidecar)
            {
                var legacy = await repository.ReadAsync(501);
                Assert.Equal(SqlWorldStateReadStatus.Invalid, legacy.Status); Assert.Null(legacy.Record); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, legacy.Failure);
            }
            await AssertBundleRefusedAsync(database, corrupt, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, initial.First, 1)), SqlWorldStateFailure.InvalidStoredState);
            await AssertBundleReadAsync(database, initial.Second, 1); Assert.Equal(corrupt, await database.SnapshotWorldBundleRowsAsync());
            // Only this explicit test-admin restoration changes the deliberately damaged row.
            await database.RestoreOwnedBundleStateRowAsync(initial.First, 1);
            Assert.Equal(baseline, await database.SnapshotWorldBundleRowsAsync());
        }
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, initial.First.ItemUids.ToArray());
        await database.AddOwnedWorldUidIndexFaultAsync(79999, 501);
        var extraUid = await database.SnapshotWorldBundleRowsAsync(); Assert.Contains("uid|79999|501", extraUid);
        var extraRead = await repository.ReadBundleAsync(501);
        Assert.Equal(SqlWorldStateReadStatus.Invalid, extraRead.Status); Assert.Null(extraRead.Record); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, extraRead.Failure);
        await AssertBundleRefusedAsync(database, extraUid, repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, initial.First, 1)), SqlWorldStateFailure.InvalidStoredState);
        await AssertBundleReadAsync(database, initial.Second, 1); Assert.Equal(extraUid, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteStoredImageAndAggregateBoundsRefuseWithoutLargeClientSideFixtures() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await database.RequireOwnedCompleteBoundsCapacityAsync();
        var initial = await SeedBundlePairAsync(database, repository); var baseline = await database.SnapshotWorldBundleRowsAsync();
        foreach (var fault in new[] { OwnedBundleStoredFault.Oversized, OwnedBundleStoredFault.AggregateBounds })
        {
            await database.ApplyOwnedBundleStoredFaultAsync(501, fault);
            var corrupt = await database.SnapshotWorldBundleBoundsAsync();
            var row = Assert.Single(corrupt, value => value.StartsWith("state|501|9001|1|2|", StringComparison.Ordinal)).Split('|');
            if (fault == OwnedBundleStoredFault.Oversized)
            { Assert.Equal("16777217", row[7]); Assert.Equal("1", row[9]); Assert.Equal("0", row[10]); }
            else
            { Assert.Equal("16777216", row[7]); Assert.Equal("1", row[10]); Assert.Equal("16777216", row[11]); Assert.Equal("1", row[13]); }
            var read = await repository.ReadBundleAsync(501);
            Assert.Equal(SqlWorldStateReadStatus.Invalid, read.Status); Assert.Null(read.Record); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, read.Failure);
            var refused = await repository.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, initial.First, 1));
            Assert.Equal(SqlWorldStateCommitStatus.Refused, refused.Status); Assert.Empty(refused.Records.ToArray()); Assert.Equal(SqlWorldStateFailure.InvalidStoredState, refused.Failure);
            Assert.Equal(corrupt, await database.SnapshotWorldBundleBoundsAsync()); await AssertBundleReadAsync(database, initial.Second, 1);
            await database.RestoreOwnedBundleStateRowAsync(initial.First, 1); Assert.Equal(baseline, await database.SnapshotWorldBundleRowsAsync());
        }
    });
}
