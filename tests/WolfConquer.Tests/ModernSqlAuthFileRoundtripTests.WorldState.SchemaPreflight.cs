using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlWorldPartialSchemaRefusesBeforeCreatingOrChangingAnyOwnedTable()
    {
        foreach (var partial in new[] { OwnedWorldPartialSchema.MarkerOnly, OwnedWorldPartialSchema.MarkerAndState,
                     OwnedWorldPartialSchema.MarkerStateAndItems, OwnedWorldPartialSchema.MalformedState })
        {
            OwnedModernSqlAuthDatabase? database = null;
            var errors = new List<Exception>();
            try
            {
                database = await OwnedModernSqlAuthDatabase.CreateAsync(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.SecondAccount);
                var accounts = await database.SnapshotAsync();
                await database.CreatePartialWorldSchemaAsync(partial);
                var before = await database.SnapshotPartialWorldSchemaAsync(partial);
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => database.CreateWorldStateRepository().InitializeSchemaAsync().AsTask());
                Assert.Equal("sqlWorldSchemaPartial", failure.Message);
                Assert.Equal(before, await database.SnapshotPartialWorldSchemaAsync(partial));
                Assert.Equal(accounts, await database.SnapshotAsync());
            }
            catch (Exception error) { errors.Add(error); }
            if (database is not null)
                try { await database.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldExistingValidSchemaReinitializationOnlyVerifiesWithoutMarkerWrite() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository);
        var rows = await database.SnapshotWorldStateRowsAsync();
        var definitions = await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion);
        await database.InstallWorldMarkerUpdateRefusalAsync();
        await repository.InitializeSchemaAsync(); // A real marker UPDATE would trigger a provider error.
        Assert.Equal(rows, await database.SnapshotWorldStateRowsAsync());
        Assert.Equal(definitions, await database.SnapshotWorldSchemaAsync(OwnedWorldSchemaFault.MarkerVersion));
    });
}
