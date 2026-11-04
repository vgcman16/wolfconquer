using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionKnownTicketRefusalsLeaveNoBindingAndExactValidTicketCanRetry() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var peer = await f.ConnectAsync(network);
        AssertSqlConnectionRefused(await f.Handler.RetainSqlWorldHandoffAsync(peer.Connection,
            f.Sql.Database.CreateWorldStateRepository(), "invalid", SqlHandoffFixture.Endpoint), SqlAccountWorldHandoffFailure.InvalidToken);
        string unknown = new('0', 64); Assert.NotEqual(offer.Token, unknown);
        AssertSqlConnectionRefused(await f.Handler.RetainSqlWorldHandoffAsync(peer.Connection,
            f.Sql.Database.CreateWorldStateRepository(), unknown, SqlHandoffFixture.Endpoint), SqlAccountWorldHandoffFailure.UnknownTicket);
        AssertSqlConnectionRefused(await f.RetainAsync(peer, offer,
            SqlHandoffFixture.Endpoint with { Port = SqlHandoffFixture.Endpoint.Port + 1 }), SqlAccountWorldHandoffFailure.EndpointMismatch);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer), offer);
        await AssertSqlConnectionReadAsync(f, peer); await f.CloseAsync(network, peer);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionServerExpiredTicketRefusesWithoutBindingAndFreshOfferCanEnter() => RunSqlConnectionAsync(async (f, network) =>
    {
        var expired = await f.Sql.RequireOfferAsync(f.Sql.CreateIssuer(TimeSpan.FromSeconds(1)));
        var peer = await f.ConnectAsync(network);
        await f.Sql.Database.WaitForSqlHandoffExpiryAsync(expired.Token);
        var before = await f.Sql.SnapshotAsync();
        AssertSqlConnectionRefused(await f.RetainAsync(peer, expired), SqlAccountWorldHandoffFailure.Expired);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        await f.Sql.Database.WaitForSqlHandoffIssuerReleaseAsync(expired.AccountId); await expired.DisposeAsync();
        var fresh = await f.Sql.RequireOfferAsync(); Assert.NotEqual(expired.Token, fresh.Token);
        AssertSqlConnectionRetained(await f.RetainAsync(peer, fresh), fresh);
        await AssertSqlConnectionReadAsync(f, peer); await f.CloseAsync(network, peer);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before, includeHandoffs: false);
    });
}
