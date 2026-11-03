using MySqlConnector;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffHeldInstallerLockRefusesAllNewTablesUntilRealRelease() => RunSqlHandoffAsync(async f =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var before = await f.SnapshotAsync();
        var objects = await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync();
        Assert.Empty(before.Handoffs); // Both names really are absent before the held-lock attempt.

        await f.Database.WithSqlHandoffInstallerLockHeldAsync(async () =>
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                f.Database.CreateWorldStateRepository().EnsureAccountHandoffSchemaAsync(deadline.Token).AsTask());
            Assert.Equal("sqlAccountHandoffSchemaInstallerBusy", error.Message);
            await AssertSqlHandoffRowsUnchangedAsync(f, before);
            Assert.Equal(objects, await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync());
        }, deadline.Token);

        // The helper requires a real RELEASE_LOCK=1 receipt and a NULL holder;
        // a new guarded connection independently confirms it before the retry.
        await f.Database.AssertSqlHandoffInstallerLockFreeAsync(deadline.Token);
        await f.Database.CreateWorldStateRepository().EnsureAccountHandoffSchemaAsync(deadline.Token);
        var installed = await f.SnapshotAsync();
        Assert.Equal(before.Accounts, installed.Accounts);
        Assert.Equal(before.World, installed.World);
        Assert.Equal(2, installed.Handoffs.Count(row => row.StartsWith("definition|", StringComparison.Ordinal)));
        Assert.Contains("wolf_sql_auth_handoff_schema|1|1", installed.Handoffs);
        Assert.Equal(3, installed.Handoffs.Length); // Two definitions and the single version row; no ticket.
        var installedObjects = await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync();
        Assert.Equal(objects.Length + 2, installedObjects.Length);
        Assert.Equal(objects, installedObjects.Where(row =>
            !row.StartsWith("table|wolf_sql_auth_handoff|", StringComparison.Ordinal) &&
            !row.StartsWith("table|wolf_sql_auth_handoff_schema|", StringComparison.Ordinal)).ToArray());
    }, initializeHandoff: false);

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffCompleteSchemaVerificationNeverUpdatesMarkerUnderRejectingTrigger() => RunSqlHandoffAsync(async f =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await f.Database.CreateSqlHandoffMarkerRejectUpdateTriggerAsync(deadline.Token);
        var before = await f.SnapshotAsync();
        var objects = await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync();
        Assert.Contains("wolf_sql_auth_handoff_schema|1|1", before.Handoffs);

        await AssertSqlHandoffMarkerUpdateRejectedAsync(f.Database, deadline.Token);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.World.EnsureAccountHandoffSchemaAsync(deadline.Token);
        await f.Database.CreateWorldStateRepository().EnsureAccountHandoffSchemaAsync(deadline.Token);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal(objects, await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync());

        // It is still an active rejecting trigger, not one the installer removed
        // or a no-op UPDATE that merely left equal values behind.
        await AssertSqlHandoffMarkerUpdateRejectedAsync(f.Database, deadline.Token);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal(objects, await f.Database.SnapshotSqlHandoffSchemaSafetyObjectsAsync());
    });

    private static async Task AssertSqlHandoffMarkerUpdateRejectedAsync(OwnedModernSqlAuthDatabase database, CancellationToken token)
    {
        var error = await Assert.ThrowsAsync<MySqlException>(() => database.AttemptSqlHandoffMarkerNoOpUpdateAsync(token));
        Assert.Equal(1644, error.Number); // Actual server SIGNAL, not a fixture-generated exception.
        Assert.Contains("owned_handoff_marker_update_forbidden", error.Message, StringComparison.Ordinal);
    }
}
