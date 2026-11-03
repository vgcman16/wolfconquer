using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffMyIsamAccountsRefusesIssueAndRedeemWithoutPublishingOrConsumption()
    {
        await RunSqlHandoffAsync(AssertSqlHandoffIssueRejectsNontransactionalAccountsAsync);
        await RunSqlHandoffAsync(AssertSqlHandoffRedeemRejectsNontransactionalAccountsAsync);
    }

    private static async Task AssertSqlHandoffIssueRejectsNontransactionalAccountsAsync(SqlHandoffFixture f)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var before = await f.SnapshotAsync();
        Assert.Equal((0L, 0L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
        await f.Database.UseOwnedSqlHandoffMyIsamAccountsAsync(deadline.Token);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        string definition = await f.Database.SnapshotOwnedSqlHandoffAccountsDefinitionAsync(deadline.Token);

        SqlAccountWorldHandoffIssueResult? returned = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            returned = await f.IssueAsync(token: deadline.Token);
        });
        Assert.Equal("sqlHandoffAccountsEngineUnsupported", error.Message);
        Assert.Null(returned);
        await f.Database.AssertOwnedSqlHandoffAccountsIsMyIsamAsync(deadline.Token);
        Assert.Equal(definition, await f.Database.SnapshotOwnedSqlHandoffAccountsDefinitionAsync(deadline.Token));
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal((0L, 0L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
    }

    private static async Task AssertSqlHandoffRedeemRejectsNontransactionalAccountsAsync(SqlHandoffFixture f)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var offer = await f.RequireOfferAsync();
        var issuerId = await f.Database.ObserveAuthenticationHolderAsync(offer.AccountId);
        Assert.NotNull(issuerId);
        var before = await f.SnapshotAsync();
        Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());

        // RequireOfferAsync has joined issuance and its transaction disposal.
        // The live offer's watcher polls the ticket without an account transaction.
        await f.Database.UseOwnedSqlHandoffMyIsamAccountsAsync(deadline.Token);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        string definition = await f.Database.SnapshotOwnedSqlHandoffAccountsDefinitionAsync(deadline.Token);
        SqlAccountWorldHandoffRedeemResult? returned = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            returned = await f.RedeemAsync(offer, token: deadline.Token);
        });
        Assert.Equal("sqlHandoffAccountsEngineUnsupported", error.Message);
        Assert.Null(returned);
        await f.Database.AssertOwnedSqlHandoffAccountsIsMyIsamAsync(deadline.Token);
        Assert.Equal(definition, await f.Database.SnapshotOwnedSqlHandoffAccountsDefinitionAsync(deadline.Token));
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());

        // Join the real watcher and issuer close, rather than abandoning the
        // offer after a refused redemption. The fixture also retains it for cleanup.
        await offer.DisposeAsync();
        await f.Database.AssertAuthenticationClaimsReleasedAsync(offer.AccountId);
        await f.Database.AssertSqlHandoffOriginalIssuerSessionGoneAsync(issuerId!.Value);
        Assert.Equal(definition, await f.Database.SnapshotOwnedSqlHandoffAccountsDefinitionAsync(deadline.Token));
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
    }
}
