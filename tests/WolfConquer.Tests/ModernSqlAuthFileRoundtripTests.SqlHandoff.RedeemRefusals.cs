using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffInvalidAndUnknownTokensCannotConsumeTheRealOffer() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var before = await f.SnapshotAsync();
        foreach (string invalid in new[] { "", "bad", new string('a', 64), new string('A', 63) })
        {
            AssertSqlHandoffRedeemRefused(await f.RedeemTokenAsync(invalid), SqlAccountWorldHandoffFailure.InvalidToken);
            await AssertSqlHandoffRowsUnchangedAsync(f, before);
        }
        AssertSqlHandoffRedeemRefused(await f.RedeemTokenAsync(new string('A', 64)), SqlAccountWorldHandoffFailure.UnknownTicket);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(offer));
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffWrongEndpointDoesNotConsumeOrPublishAndCorrectEndpointRetries()
    {
        foreach (var endpoint in new[]
        {
            SqlHandoffFixture.Endpoint with { Address = "127.0.0.2" },
            SqlHandoffFixture.Endpoint with { Port = SqlHandoffFixture.Endpoint.Port + 1 }
        })
            await RunSqlHandoffAsync(async f =>
            {
                var offer = await f.RequireOfferAsync();
                var before = await f.SnapshotAsync();
                AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer, endpoint), SqlAccountWorldHandoffFailure.EndpointMismatch);
                await AssertSqlHandoffRowsUnchangedAsync(f, before);
                Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
                await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(offer));
                await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffRechecksCurrentIdentityCredentialAndRequiredPermissions()
    {
        foreach (var change in new[]
        {
            (AccountColumn.Password, (object?)"changed-owned-handoff-password", SqlAccountWorldHandoffFailure.IdentityConflict),
            (AccountColumn.Username, (object?)"renamed-owned-handoff-account", SqlAccountWorldHandoffFailure.IdentityConflict),
            (AccountColumn.EntityId, (object?)9003L, SqlAccountWorldHandoffFailure.IdentityConflict),
            (AccountColumn.BannedId, (object?)1L, SqlAccountWorldHandoffFailure.Banned),
            (AccountColumn.Online, (object?)-7L, SqlAccountWorldHandoffFailure.AlreadyOnline),
            (AccountColumn.BannedId, (object?)null, SqlAccountWorldHandoffFailure.InvalidAccountState),
            (AccountColumn.Online, (object?)null, SqlAccountWorldHandoffFailure.InvalidAccountState)
        })
            await RunSqlHandoffAsync(async f =>
            {
                var offer = await f.RequireOfferAsync();
                await f.Database.SetAuthenticationAccountColumnAsync(offer.AccountId, change.Item1, change.Item2);
                var before = await f.SnapshotAsync();
                AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), change.Item3);
                await AssertSqlHandoffRowsUnchangedAsync(f, before);
                Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffRechecksCurrentCompleteWorldUidWithoutConsumingOffer() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        await f.Database.ReplaceSqlHandoffWorldIdentityAsync(offer.AccountId, 9003);
        var raw = await f.RequireWorldOwnerAsync(offer.AccountId);
        Assert.Equal(9003u, raw.CharacterUid);
        var valid = await raw.ReadBundleAsync();
        Assert.Equal(SqlWorldStateReadStatus.Found, valid.Status);
        Assert.Equal(SqlWorldStateFailure.None, valid.Failure);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(valid.Record);
        Assert.Equal(offer.AccountId, record.Image.AccountId);
        Assert.Equal(9003u, record.Image.CharacterUid);
        Assert.Equal(4, record.Image.Sidecars.Length);
        await raw.DisposeAsync();
        var before = await f.SnapshotAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.IdentityConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffExistingWorldOwnerConflictPreservesTokenForActualRetry() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var occupied = await f.RequireWorldOwnerAsync(offer.AccountId);
        var before = await f.SnapshotAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.SessionConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await occupied.DisposeAsync();
        await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(offer));
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffMismatchedStoredRedemptionPairIsInvalidDataAndPublishesNoWorldOwner() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        // Join the actual watcher before intentionally corrupting its row. This
        // tests stored-row validation before issuer liveness without accepting
        // or discarding a concurrent background InvalidDataException.
        await offer.DisposeAsync();
        await f.Database.BreakSqlHandoffRedemptionPairAsync(offer.Token);
        var before = await f.SnapshotAsync();
        SqlAccountWorldHandoffRedeemResult? returned = null;
        await Assert.ThrowsAsync<InvalidDataException>(async () => { returned = await f.RedeemAsync(offer); });
        Assert.Null(returned);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
    });
}
