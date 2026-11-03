using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    // Repository-component controls in the existing closed opt-in SQL family.
    // This runner does not claim native admission or a SQL-backed live World.
    private static async Task RunSqlWorldStateAsync(Func<OwnedModernSqlAuthDatabase, SqlWorldStateRepository, Task> body)
    {
        OwnedModernSqlAuthDatabase? database = null;
        var errors = new List<Exception>();
        try
        {
            database = await OwnedModernSqlAuthDatabase.CreateAsync(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.SecondAccount);
            var accounts = await database.SnapshotAsync();
            var repository = database.CreateWorldStateRepository();
            await repository.InitializeSchemaAsync();
            await body(database, repository);
            Assert.Equal(accounts, await database.SnapshotAsync());
        }
        catch (Exception error) { errors.Add(error); }
        if (database is not null)
            try { await database.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static SqlWorldStateRequest WorldRequest(SqlWorldStateOperationKind kind, SqlWorldStateImage image, long revision, Guid? id = null) =>
        SqlWorldStateTestData.Request(kind, id ?? Guid.NewGuid(), new SqlWorldStateChange(image, revision));

    private static SqlWorldStateRequest WorldPair(SqlWorldStateImage first, SqlWorldStateImage second, long firstRevision = 1,
        long secondRevision = 1, Guid? id = null) => SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair,
            id ?? Guid.NewGuid(), new(second, secondRevision), new(first, firstRevision)); // Deliberately reversed input.

    private static void AssertWorldImage(SqlWorldStateImage expected, SqlWorldStateImage actual)
    {
        Assert.Equal(expected.AccountId, actual.AccountId); Assert.Equal(expected.CharacterUid, actual.CharacterUid);
        Assert.Equal(expected.CopyCharacterBytes(), actual.CopyCharacterBytes());
        Assert.Equal(expected.CopyItemBytes(), actual.CopyItemBytes());
        Assert.Equal(expected.ItemUids.ToArray(), actual.ItemUids.ToArray());
    }

    private static void AssertWorldCommit(SqlWorldStateCommitResult result, SqlWorldStateCommitStatus status,
        long revision, params SqlWorldStateImage[] images)
    {
        Assert.Equal(status, result.Status); Assert.Equal(SqlWorldStateFailure.None, result.Failure);
        var records = result.Records.ToArray(); Assert.Equal(images.Length, records.Length);
        for (int index = 0; index < images.Length; index++)
        { Assert.Equal(revision, records[index].Revision); AssertWorldImage(images[index], records[index].Image); }
    }

    private static async Task AssertWorldReadAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateImage image, long revision)
    {
        var result = await database.CreateWorldStateRepository().ReadAsync(image.AccountId); // New repository/connection; SQL-only.
        Assert.Equal(SqlWorldStateReadStatus.Found, result.Status); Assert.Equal(SqlWorldStateFailure.None, result.Failure);
        var record = Assert.IsType<SqlWorldStateRecord>(result.Record);
        Assert.Equal(revision, record.Revision); AssertWorldImage(image, record.Image);
    }

    private static async Task SeedWorldPairAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateRepository repository)
    {
        var initial = SqlWorldStateTestData.Initial();
        AssertWorldCommit(await repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, initial.First, 0)),
            SqlWorldStateCommitStatus.Committed, 1, initial.First);
        AssertWorldCommit(await repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, initial.Second, 0)),
            SqlWorldStateCommitStatus.Committed, 1, initial.Second);
        await AssertWorldReadAsync(database, initial.First, 1); await AssertWorldReadAsync(database, initial.Second, 1);
    }

    private static async Task AssertWorldRefusedAsync(OwnedModernSqlAuthDatabase database, string[] before,
        ValueTask<SqlWorldStateCommitResult> operation, SqlWorldStateFailure expected)
    {
        var result = await operation;
        Assert.Equal(SqlWorldStateCommitStatus.Refused, result.Status); Assert.Equal(expected, result.Failure);
        Assert.Empty(result.Records.ToArray());
        Assert.Equal(before, await database.SnapshotWorldStateRowsAsync());
    }
}
