using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionRetainsCompleteWorldUntilRealPeerEofWithoutPublishingGameplay() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var peer = await f.ConnectAsync(network);
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer), offer);
        Assert.Equal((1L, 0L, 1L), await f.Sql.Database.ObserveSqlHandoffCountsAsync());
        await AssertSqlConnectionReadAsync(f, peer); await f.AssertExcludedAsync(offer.AccountId);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before, includeHandoffs: false);
        var committed = await f.Sql.SnapshotAsync();
        await offer.DisposeAsync(); await f.Sql.Database.WaitForSqlHandoffIssuerReleaseAsync(offer.AccountId);
        // The account issuer has genuinely ended, while this exact world owner
        // remains tied to the still-open actual native TCP connection.
        await AssertSqlConnectionReadAsync(f, peer); await f.AssertExcludedAsync(offer.AccountId);
        await f.CloseAsync(network, peer);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        var fresh = await f.Sql.RequireWorldOwnerAsync(offer.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await fresh.ReadBundleAsync()).Status);
        await fresh.DisposeAsync(); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, committed);
        AssertSqlHandoffRedeemRefused(await f.Sql.RedeemAsync(offer), SqlAccountWorldHandoffFailure.AlreadyRedeemed);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionSecondAccountTicketIsNotConsumedByBoundOrForeignFrame() => RunSqlConnectionAsync(async (f, network) =>
    {
        var first = await f.Sql.RequireOfferAsync();
        var second = await f.Sql.RequireOfferAsync(username: OwnedModernSqlAuthDatabase.SecondUsername);
        var peer = await f.ConnectAsync(network); AssertSqlConnectionRetained(await f.RetainAsync(peer, first), first);
        var before = await f.Sql.SnapshotAsync();
        AssertSqlConnectionRefused(await f.RetainAsync(peer, second), SqlAccountWorldHandoffFailure.SessionConflict);
        using var log = new NativePlayerDiagnosticLog();
        var foreign = new WolfConquer.Core.World.WorldServerFrameHandler(snapshotLoader: null,
            protocolProfile: WolfConquer.Core.Runtime.ConquerProtocolProfile.VisibleDx8D13, log: log);
        AssertSqlConnectionRefused(await foreign.RetainSqlWorldHandoffAsync(peer.Connection,
            f.Sql.Database.CreateWorldStateRepository(), second.Token, SqlHandoffFixture.Endpoint), SqlAccountWorldHandoffFailure.SessionConflict);
        await AssertSqlConnectionReadAsync(f, peer); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        var other = await f.ConnectAsync(network);
        // A separate real connection can consume that exact pending second
        // ticket; the two earlier refusals did not consume its authorization.
        AssertSqlConnectionRetained(await f.RetainAsync(other, second), second);
        Assert.Equal((2L, 0L, 2L), await f.Sql.Database.ObserveSqlHandoffCountsAsync());
        await f.AssertExcludedAsync(first.AccountId); await f.AssertExcludedAsync(second.AccountId);
        await f.CloseAsync(network, peer); await f.AssertExcludedAsync(second.AccountId);
        await f.CloseAsync(network, other); log.AssertNoWorldRuntimeDiagnostics();
        var fresh = await f.Sql.RequireWorldOwnerAsync(first.AccountId); await fresh.DisposeAsync();
        var otherFresh = await f.Sql.RequireWorldOwnerAsync(second.AccountId); await otherFresh.DisposeAsync();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionExplicitReleaseIsIdempotentButCannotRebindAnotherAccount() => RunSqlConnectionAsync(async (f, network) =>
    {
        var first = await f.Sql.RequireOfferAsync();
        var second = await f.Sql.RequireOfferAsync(username: OwnedModernSqlAuthDatabase.SecondUsername);
        var peer = await f.ConnectAsync(network); AssertSqlConnectionRetained(await f.RetainAsync(peer, first), first);
        var before = await f.Sql.SnapshotAsync();
        await f.Handler.ReleaseRetainedSqlWorldHandoffAsync(peer.Connection);
        await f.Handler.ReleaseRetainedSqlWorldHandoffAsync(peer.Connection);
        Assert.False(peer.Connection.HasObservedTransportClose);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        AssertSqlConnectionRefused(await f.RetainAsync(peer, second), SqlAccountWorldHandoffFailure.SessionConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        var fresh = await f.Sql.RequireWorldOwnerAsync(first.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await fresh.ReadBundleAsync()).Status);
        await fresh.DisposeAsync(); await f.CloseAsync(network, peer);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
    });
}
