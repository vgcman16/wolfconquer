using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionCallerCancellationDoesNotConsumeBeforeEntryOrPretendToBeEof() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var peer = await f.ConnectAsync(network);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        SqlConnectionHandoffResult? returned = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        { returned = await f.RetainAsync(peer, offer, token: cancelled.Token); });
        Assert.Null(returned); Assert.False(peer.Connection.HasObservedTransportClose);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);

        using var callerScope = new CancellationTokenSource();
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer, token: callerScope.Token), offer);
        var committed = await f.Sql.SnapshotAsync(); await callerScope.CancelAsync();
        Assert.False(peer.Connection.HasObservedTransportClose);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        { await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection, cancelled.Token); });
        await AssertSqlConnectionReadAsync(f, peer); await f.AssertExcludedAsync(offer.AccountId);
        await peer.Wire.SendAsync(new PacketWriter(65001, 0).ToArray(), network.Token);
        var dispatch = await network.NextAsync(); Assert.Same(peer.Connection, dispatch.Connection);
        Assert.Null(dispatch.Before); Assert.Null(dispatch.After);
        await AssertSqlConnectionReadAsync(f, peer); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, committed);
        await f.CloseAsync(network, peer);
        var fresh = await f.Sql.RequireWorldOwnerAsync(offer.AccountId); await fresh.DisposeAsync();
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, committed);
    });
}
