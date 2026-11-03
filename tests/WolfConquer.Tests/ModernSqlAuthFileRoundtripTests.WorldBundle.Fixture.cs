using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    // Synthetic storage only: no native/authentication receipt or live backend is issued.
    private static SqlWorldStateBundle CompleteWorldImage(SqlWorldStateImage core, int marker = 0)
    {
        var spells = SqlWorldStateBundleTests.Spells(); spells[8] = checked((byte)marker);
        var proficiencies = SqlWorldStateBundleTests.Proficiencies(); proficiencies[16] = checked((byte)(4 + marker));
        var quests = SqlWorldStateBundleTests.Quests(); quests[20] = checked((byte)(5 + marker));
        var gear = new CombatGearSnapshot(checked((uint)(11 + marker)), 22, 33, 12, 1, new(9, 8, 7, 6, 5, 4, 3),
            [new CombatGearSnapshotItem(701, 4, 123, 1, 2, 928000,
                [new uint[] { 101, 102, 103, 104, 105, 106, 107 }, new uint[] { 201, 202, 203, 204, 205, 206, 207 }],
                new uint[] { 11, 12, 13, 14, 15, 16 })], new(2, new uint[] { 301, 302 }));
        return SqlWorldStateBundleTests.Bundle(core,
        [
            new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, spells),
            new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, proficiencies),
            new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, quests),
            new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(gear))),
        ]);
    }

    private static SqlWorldStateBundleRequest BundleRequest(SqlWorldStateOperationKind kind, SqlWorldStateBundle image, long revision, Guid? id = null) =>
        BundleRequestMany(kind, id ?? Guid.NewGuid(), new SqlWorldStateBundleChange(image, revision));

    private static SqlWorldStateBundleRequest BundlePair(SqlWorldStateBundle first, SqlWorldStateBundle second,
        long firstRevision = 1, long secondRevision = 1, Guid? id = null) =>
        BundleRequestMany(SqlWorldStateOperationKind.SavePair, id ?? Guid.NewGuid(), new(second, secondRevision), new(first, firstRevision));

    private static SqlWorldStateBundleRequest BundleRequestMany(SqlWorldStateOperationKind kind, Guid id, params SqlWorldStateBundleChange[] changes)
    {
        Assert.True(SqlWorldStateBundleRequest.TryCreate(id, kind, changes, out var request, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateFailure.None, failure); return Assert.IsType<SqlWorldStateBundleRequest>(request);
    }

    private static void AssertBundleImage(SqlWorldStateBundle expected, SqlWorldStateBundle actual)
    {
        AssertWorldImage(expected.Core, actual.Core); Assert.Equal(expected.TotalImageBytes, actual.TotalImageBytes);
        Assert.Equal(4, actual.Sidecars.Length);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(expected.Sidecars[index].Kind, actual.Sidecars[index].Kind);
            Assert.Equal(expected.Sidecars[index].Presence, actual.Sidecars[index].Presence);
            Assert.Equal(expected.Sidecars[index].CopyBytes(), actual.Sidecars[index].CopyBytes());
        }
    }

    private static void AssertBundleCommit(SqlWorldStateBundleCommitResult result, SqlWorldStateCommitStatus status,
        long revision, params SqlWorldStateBundle[] images)
    {
        Assert.Equal(status, result.Status); Assert.Equal(SqlWorldStateFailure.None, result.Failure);
        var records = result.Records.ToArray(); Assert.Equal(images.Length, records.Length);
        for (int index = 0; index < images.Length; index++)
        { Assert.Equal(revision, records[index].Revision); AssertBundleImage(images[index], records[index].Image); }
    }

    private static async Task AssertBundleReadAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateBundle image, long revision)
    {
        var result = await database.CreateWorldStateRepository().ReadBundleAsync(image.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, result.Status); Assert.Equal(SqlWorldStateFailure.None, result.Failure);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(result.Record);
        Assert.Equal(revision, record.Revision); AssertBundleImage(image, record.Image);
    }

    private static async Task<(SqlWorldStateBundle First, SqlWorldStateBundle Second)> SeedBundlePairAsync(OwnedModernSqlAuthDatabase database, SqlWorldStateRepository repository)
    {
        var core = SqlWorldStateTestData.Initial(); var first = CompleteWorldImage(core.First); var second = CompleteWorldImage(core.Second);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, first, 0)), SqlWorldStateCommitStatus.Committed, 1, first);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, second, 0)), SqlWorldStateCommitStatus.Committed, 1, second);
        await AssertBundleReadAsync(database, first, 1); await AssertBundleReadAsync(database, second, 1);
        return (first, second);
    }

    private static async Task AssertBundleRefusedAsync(OwnedModernSqlAuthDatabase database, string[] before,
        ValueTask<SqlWorldStateBundleCommitResult> operation, SqlWorldStateFailure expected)
    {
        var result = await operation;
        Assert.Equal(SqlWorldStateCommitStatus.Refused, result.Status); Assert.Equal(expected, result.Failure); Assert.Empty(result.Records.ToArray());
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    }

    private static async Task AssertCoreRefusedWithoutBundleChangesAsync(OwnedModernSqlAuthDatabase database, string[] before,
        ValueTask<SqlWorldStateCommitResult> operation, SqlWorldStateFailure expected)
    {
        var result = await operation;
        Assert.Equal(SqlWorldStateCommitStatus.Refused, result.Status); Assert.Equal(expected, result.Failure); Assert.Empty(result.Records.ToArray());
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    }
}
