using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffBindsActualOwnedSchemaAndCannotRedeemFromAnotherOwnedSchema() => RunSqlHandoffAsync(async f =>
    {
        var foreign = await f.CreateOtherDatabaseAsync();
        var before = await f.SnapshotAsync();
        var foreignAccounts = await foreign.SnapshotAuthenticationRowsAsync();
        var foreignWorld = await foreign.SnapshotSqlHandoffWorldRowsAsync();
        var foreignHandoffs = await foreign.SnapshotSqlHandoffTablesAsync();
        Assert.NotEqual(f.Database.SchemaName, foreign.SchemaName);
        // Both schemas are actual nonce-owned databases on this configured test
        // endpoint. No second physical-server isolation is claimed.
        AssertSqlHandoffIssueRefused(await f.IssueAsync(f.CreateIssuer(worldDatabase: foreign)), SqlAccountWorldHandoffFailure.DomainConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal(foreignAccounts, await foreign.SnapshotAuthenticationRowsAsync());
        Assert.Equal(foreignWorld, await foreign.SnapshotSqlHandoffWorldRowsAsync());
        Assert.Equal(foreignHandoffs, await foreign.SnapshotSqlHandoffTablesAsync());

        var offer = await f.RequireOfferAsync();
        var offered = await f.SnapshotAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer, repository: foreign.CreateWorldStateRepository()),
            SqlAccountWorldHandoffFailure.UnknownTicket);
        await AssertSqlHandoffRowsUnchangedAsync(f, offered);
        Assert.Equal(foreignAccounts, await foreign.SnapshotAuthenticationRowsAsync());
        Assert.Equal(foreignWorld, await foreign.SnapshotSqlHandoffWorldRowsAsync());
        Assert.Equal(foreignHandoffs, await foreign.SnapshotSqlHandoffTablesAsync());
        await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(offer));
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });
}
