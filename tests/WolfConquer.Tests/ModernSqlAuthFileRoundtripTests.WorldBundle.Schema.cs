using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHistoricalSchemaOneRefusesBothReadersAndInitializationWithoutAlterOrRowChanges() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository); await database.MakeOwnedWorldSchemaOneAsync();
        var rows = await database.SnapshotWorldStateRowsAsync();
        var definitions = await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion);
        Assert.Contains("schema|1|1", rows);
        var state = Assert.Single(definitions, row => row.StartsWith("wolf_sql_world_state|", StringComparison.Ordinal));
        foreach (string column in new[] { "record_format", "spell_image", "proficiency_image", "quest_image", "combat_gear_image" }) Assert.DoesNotContain(column, state, StringComparison.Ordinal);
        var ordinary = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadAsync(501).AsTask());
        Assert.Equal("sqlWorldSchemaColumnsMismatch", ordinary.Message);
        var complete = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadBundleAsync(501).AsTask());
        Assert.Equal("sqlWorldSchemaColumnsMismatch", complete.Message);
        var initialize = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InitializeSchemaAsync().AsTask());
        Assert.Equal("sqlWorldSchemaColumnsMismatch", initialize.Message);
        var create = BundleRequest(SqlWorldStateOperationKind.Create, CompleteWorldImage(SqlWorldStateTestData.Image(503, 9003)), 0);
        var mutation = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateBundleAsync(create).AsTask());
        Assert.Equal("sqlWorldSchemaColumnsMismatch", mutation.Message);
        Assert.Equal(rows, await database.SnapshotWorldStateRowsAsync());
        Assert.Equal(definitions, await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion));
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlSchemaTwoCompleteColumnTypeDriftRefusesWithoutRepairingSavedBundles()
    {
        foreach (var fault in new[] { OwnedBundleSchemaFault.SignedFormat, OwnedBundleSchemaFault.MediumSpellBlob })
        {
            await RunSqlWorldStateAsync(async (database, repository) =>
            {
                await SeedBundlePairAsync(database, repository); await database.ApplyOwnedCompleteSchemaFaultAsync(fault);
                var rows = await database.SnapshotWorldBundleRowsAsync(); var definitions = await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion);
                var read = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadBundleAsync(501).AsTask());
                Assert.Equal("sqlWorldSchemaColumnsMismatch", read.Message);
                var initialize = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InitializeSchemaAsync().AsTask());
                Assert.Equal("sqlWorldSchemaColumnsMismatch", initialize.Message);
                Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync()); Assert.Equal(definitions, await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion));
            });
        }
    }
}
