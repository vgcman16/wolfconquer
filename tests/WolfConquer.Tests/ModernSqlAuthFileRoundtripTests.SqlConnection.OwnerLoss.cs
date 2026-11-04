using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionKilledWorldOwnerRetiresWithCommittedFailureAndCannotAdoptReplacement() => RunSqlConnectionAsync(async (f, network) =>
    {
        var first = await f.Sql.RequireOfferAsync();
        var second = await f.Sql.RequireOfferAsync(username: OwnedModernSqlAuthDatabase.SecondUsername);
        var peer = await f.ConnectAsync(network); AssertSqlConnectionRetained(await f.RetainAsync(peer, first), first);
        await first.DisposeAsync(); await f.Sql.Database.WaitForSqlHandoffIssuerReleaseAsync(first.AccountId);
        var before = await f.Sql.SnapshotAsync();
        await f.Sql.Database.KillOwnedWorldCustodyConnectionAsync(first.AccountId);
        // A genuinely new physical owner can now acquire the released SQL
        // claim; it cannot revive the dead connection's private Frame binding.
        var replacement = await f.Sql.RequireWorldOwnerAsync(first.AccountId);
        var failure = await Assert.ThrowsAsync<SqlConnectionHandoffCommittedException>(async () =>
        { await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection); });
        Assert.NotNull(failure.InnerException);
        var replayedFailure = await Assert.ThrowsAsync<SqlConnectionHandoffCommittedException>(async () =>
        { await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection); });
        Assert.Same(failure, replayedFailure);
        AssertSqlConnectionRefused(await f.RetainAsync(peer, second), SqlAccountWorldHandoffFailure.SessionConflict);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await replacement.ReadBundleAsync()).Status);
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before); await f.CloseAsync(network, peer);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await replacement.ReadBundleAsync()).Status);
        await replacement.DisposeAsync(); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionChangedWorldIdentityCannotPublishHistoricalReadOrRewriteNewState() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var peer = await f.ConnectAsync(network);
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer), offer); await AssertSqlConnectionReadAsync(f, peer);
        await offer.DisposeAsync(); await f.Sql.Database.WaitForSqlHandoffIssuerReleaseAsync(offer.AccountId);
        await f.Sql.Database.ReplaceSqlHandoffWorldIdentityAsync(offer.AccountId, 9003);
        var changed = await f.Sql.SnapshotAsync();
        await Assert.ThrowsAsync<SqlConnectionHandoffCommittedException>(async () =>
        { await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection); });
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, changed);
        var actual = await f.Sql.RequireWorldOwnerAsync(offer.AccountId);
        Assert.Equal(9003U, actual.CharacterUid);
        var read = await actual.ReadBundleAsync(); Assert.Equal(SqlWorldStateReadStatus.Found, read.Status);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(read.Record); Assert.Equal(1L, record.Revision);
        var expected = CompleteWorldImage(SqlWorldStateTestData.Image(offer.AccountId, 9003, SqlWorldStateTestData.FirstItems()));
        AssertBundleImage(expected, record.Image);
        await f.CloseAsync(network, peer); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, changed);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await actual.ReadBundleAsync()).Status);
        await actual.DisposeAsync();
    });
}
