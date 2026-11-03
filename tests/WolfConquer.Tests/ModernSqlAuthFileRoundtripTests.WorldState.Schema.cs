namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlWorldSchemaDriftRefusesReadAndInitializationWithoutRepairingRowsOrShape()
    {
        foreach (var fault in new[] { OwnedWorldSchemaFault.MarkerVersion, OwnedWorldSchemaFault.VersionWidth,
                     OwnedWorldSchemaFault.MarkerEngine, OwnedWorldSchemaFault.ExtraForeignKey })
        {
            await RunSqlWorldStateAsync(async (database, repository) =>
            {
                await SeedWorldPairAsync(database, repository);
                await database.ApplyWorldSchemaFaultAsync(fault);
                var rows = await database.SnapshotWorldStateRowsAsync();
                var definitions = await database.SnapshotWorldSchemaAsync(fault);
                string expected = fault switch
                {
                    OwnedWorldSchemaFault.MarkerVersion => "sqlWorldSchemaVersionMismatch",
                    OwnedWorldSchemaFault.VersionWidth => "sqlWorldSchemaColumnsMismatch",
                    OwnedWorldSchemaFault.MarkerEngine => "sqlWorldSchemaEngineMismatch",
                    OwnedWorldSchemaFault.ExtraForeignKey => "sqlWorldSchemaForeignKeyMismatch",
                    _ => throw new ArgumentOutOfRangeException(nameof(fault))
                };
                var readFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.ReadAsync(SqlWorldStateTestData.FirstAccount).AsTask());
                Assert.Equal(expected, readFailure.Message);
                Assert.Equal(rows, await database.SnapshotWorldStateRowsAsync());
                Assert.Equal(definitions, await database.SnapshotWorldSchemaAsync(fault));
                var initializeFailure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.InitializeSchemaAsync().AsTask());
                Assert.Equal(expected, initializeFailure.Message);
                Assert.Equal(rows, await database.SnapshotWorldStateRowsAsync());
                Assert.Equal(definitions, await database.SnapshotWorldSchemaAsync(fault));
            });
        }
    }
}
