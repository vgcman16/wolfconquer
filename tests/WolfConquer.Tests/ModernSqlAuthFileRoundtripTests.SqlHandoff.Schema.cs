using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffExplicitInstallerCreatesOnlyCompleteSchemaAndVerifiesWithoutMarkerWrites() => RunSqlHandoffAsync(async f =>
    {
        var before = await f.SnapshotAsync();
        Assert.Empty(before.Handoffs);
        await f.World.EnsureAccountHandoffSchemaAsync();
        var installed = await f.SnapshotAsync();
        Assert.Equal(before.Accounts, installed.Accounts); Assert.Equal(before.World, installed.World);
        Assert.Equal(2, installed.Handoffs.Count(row => row.StartsWith("definition|", StringComparison.Ordinal)));
        Assert.Contains("wolf_sql_auth_handoff_schema|1|1", installed.Handoffs);
        await f.Database.CreateWorldStateRepository().EnsureAccountHandoffSchemaAsync();
        await AssertSqlHandoffRowsUnchangedAsync(f, installed);
        await f.RequireOfferAsync();
        var offered = await f.SnapshotAsync();
        await f.Database.CreateWorldStateRepository().EnsureAccountHandoffSchemaAsync();
        await AssertSqlHandoffRowsUnchangedAsync(f, offered);
    }, initializeHandoff: false);

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffPartialSchemaRefusesBeforeDdlOrOfferPublication() => RunSqlHandoffAsync(async f =>
    {
        await f.Database.ApplySqlHandoffSchemaFaultAsync(OwnedSqlHandoffSchemaFault.MarkerOnly);
        await AssertSqlHandoffSchemaRefusedWithoutChangesAsync(f, "sqlAccountHandoffSchemaPartial");
    }, initializeHandoff: false);

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffVersionEngineAndPrimaryKeyFaultsRefuseWithoutAutoRepair()
    {
        foreach (var fault in new[]
        {
            (OwnedSqlHandoffSchemaFault.Version, "sqlAccountHandoffSchemaVersionMismatch"),
            (OwnedSqlHandoffSchemaFault.Engine, "sqlAccountHandoffSchemaEngineMismatch"),
            (OwnedSqlHandoffSchemaFault.PrimaryKey, "sqlAccountHandoffSchemaIndexesMismatch")
        })
            await RunSqlHandoffAsync(async f =>
            {
                await f.Database.ApplySqlHandoffSchemaFaultAsync(fault.Item1);
                await AssertSqlHandoffSchemaRefusedWithoutChangesAsync(f, fault.Item2);
            });
    }

    private static async Task AssertSqlHandoffSchemaRefusedWithoutChangesAsync(SqlHandoffFixture f, string expectedMessage)
    {
        var before = await f.SnapshotAsync();
        var installer = await Assert.ThrowsAsync<InvalidOperationException>(() => f.World.EnsureAccountHandoffSchemaAsync().AsTask());
        Assert.Equal(expectedMessage, installer.Message);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        SqlAccountWorldHandoffIssueResult? issued = null;
        var issue = await Assert.ThrowsAsync<InvalidOperationException>(async () => { issued = await f.IssueAsync(); });
        Assert.Equal(expectedMessage, issue.Message); Assert.Null(issued);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        SqlAccountWorldHandoffRedeemResult? redeemed = null;
        var redeem = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            redeemed = await f.RedeemTokenAsync(new string('A', 64));
        });
        Assert.Equal(expectedMessage, redeem.Message); Assert.Null(redeemed);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
    }
}
