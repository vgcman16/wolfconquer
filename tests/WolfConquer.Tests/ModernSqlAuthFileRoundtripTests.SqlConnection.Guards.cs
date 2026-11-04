using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionPeerEofRefusesBeforeConsumingAnOtherwiseValidTicket() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var closed = await f.ConnectAsync(network); await f.CloseAsync(network, closed);
        AssertSqlConnectionRefused(await f.RetainAsync(closed, offer), SqlAccountWorldHandoffFailure.SessionRetired);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(closed.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        var fresh = await f.ConnectAsync(network);
        AssertSqlConnectionRetained(await f.RetainAsync(fresh, offer), offer);
        await AssertSqlConnectionReadAsync(f, fresh); await f.CloseAsync(network, fresh);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionRequiresItsOwnRealNativeWireAndFrameEofLifetime() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var peer = await f.ConnectAsync(network, establishFrameLifetime: false); int syntheticSends = 0;
        // A genuine DH-born native socket is insufficient before its actual
        // primary Frame has established the exact EOF lifecycle callback.
        AssertSqlConnectionRefused(await f.RetainAsync(peer, offer), SqlAccountWorldHandoffFailure.SessionConflict);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        AssertSqlConnectionNativeUnchanged(peer.Before, peer.Connection); f.AssertFilesUnchanged();
        // Negative control only: borrowing the native object cannot mint its
        // producer's private wire or the Frame's EOF ownership.
        var synthetic = new ConquerTcpConnection(null, null,
            (_, _) => { syntheticSends++; return ValueTask.CompletedTask; },
            _ => ValueTask.CompletedTask, peer.Connection.NativeGameClient);
        AssertSqlConnectionRefused(await f.Handler.RetainSqlWorldHandoffAsync(synthetic,
            f.Sql.Database.CreateWorldStateRepository(), offer.Token, SqlHandoffFixture.Endpoint), SqlAccountWorldHandoffFailure.WorldUnavailable);
        Assert.Equal(0, syntheticSends);
        // The birth probe is observation-only. Dispatch a real non-probe frame
        // through the primary Frame to establish its actual EOF callback.
        await peer.Wire.SendAsync(new PacketWriter(65001, 0).ToArray(), network.Token);
        var primaryDispatch = await network.NextAsync(); Assert.Same(peer.Connection, primaryDispatch.Connection);
        Assert.Null(primaryDispatch.Before); Assert.Null(primaryDispatch.After);
        using var log = new NativePlayerDiagnosticLog();
        var foreign = new WorldServerFrameHandler(snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: log);
        // A different actual Frame cannot replace the primary callback or
        // consume SQL authorization while trying to register its own lifetime.
        AssertSqlConnectionRefused(await foreign.RetainSqlWorldHandoffAsync(peer.Connection,
            f.Sql.Database.CreateWorldStateRepository(), offer.Token, SqlHandoffFixture.Endpoint), SqlAccountWorldHandoffFailure.SessionConflict);
        Assert.Null(await foreign.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        AssertSqlConnectionNativeUnchanged(peer.Before, peer.Connection); f.AssertFilesUnchanged();
        // The primary lifetime is still current: that exact pending ticket can
        // be retained on the SAME real socket after the foreign refusal.
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer), offer);
        await AssertSqlConnectionReadAsync(f, peer); await f.AssertExcludedAsync(offer.AccountId);
        await f.CloseAsync(network, peer);
        var fresh = await f.Sql.RequireWorldOwnerAsync(offer.AccountId);
        Assert.Equal(WolfConquer.Core.Data.SqlWorld.SqlWorldStateReadStatus.Found, (await fresh.ReadBundleAsync()).Status);
        await fresh.DisposeAsync(); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before, includeHandoffs: false);
        log.AssertNoWorldRuntimeDiagnostics();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionInitializedNativeStatesRefuseWithoutSpendingTheTicket() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        foreach (string state in new[] { "uid", "full-loading", "fake" })
        {
            var peer = await f.ConnectAsync(network); var native = peer.Before.Client;
            if (state == "uid") native.Player.UID = 123;
            if (state == "full-loading") native.FullLoading = true;
            if (state == "fake") native.Fake = true;
            var changed = CaptureSqlConnectionNative(peer.Connection);
            AssertSqlConnectionRefused(await f.RetainAsync(peer, offer), SqlAccountWorldHandoffFailure.WorldUnavailable);
            Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
            AssertSqlConnectionNativeUnchanged(changed, peer.Connection);
            await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
            // Restore only this owned fixture mutation before comparing EOF
            // cleanup against the original socket-born values.
            native.Player.UID = 0; native.FullLoading = false; native.Fake = false;
            await f.CloseAsync(network, peer);
        }
        var fresh = await f.ConnectAsync(network); AssertSqlConnectionRetained(await f.RetainAsync(fresh, offer), offer);
        await AssertSqlConnectionReadAsync(f, fresh); await f.CloseAsync(network, fresh);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionWrongProtocolProfileRefusesBeforeTicketConsumption() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var before = await f.Sql.SnapshotAsync();
        var peer = await f.ConnectAsync(network);
        AssertSqlConnectionRefused(await f.RetainAsync(peer, offer), SqlAccountWorldHandoffFailure.WorldUnavailable);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before); await f.CloseAsync(network, peer);
        await AssertSqlHandoffRedeemedAsync(f.Sql, await f.Sql.RedeemAsync(offer));
    }, profile: ConquerProtocolProfile.RootD11_7899);
}
