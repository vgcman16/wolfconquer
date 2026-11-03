using System.Runtime.ExceptionServices;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffPrecancelledIssueDoesNotPublishAnOfferOrRetainClaims() => RunSqlHandoffAsync(async f =>
    {
        var before = await f.SnapshotAsync();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        SqlAccountWorldHandoffIssueResult? returned = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            returned = await f.IssueAsync(token: cancelled.Token);
        });
        Assert.Null(returned);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
        await f.RequireOfferAsync();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffPrecancelledRedeemDoesNotConsumeAndCanRetry() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var before = await f.SnapshotAsync();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        SqlAccountWorldHandoffRedeemResult? returned = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            returned = await f.RedeemAsync(offer, token: cancelled.Token);
        });
        Assert.Null(returned);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(offer));
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffDisposedIssuerCannotBeRebuiltFromStoredRowOrFreshSameAccountOffer() => RunSqlHandoffAsync(async f =>
    {
        var old = await f.RequireOfferAsync();
        await old.DisposeAsync();
        await f.Database.AssertAuthenticationClaimsReleasedAsync(old.AccountId);
        var fresh = await f.RequireOfferAsync();
        Assert.False(string.Equals(old.Token, fresh.Token, StringComparison.Ordinal));
        var before = await f.SnapshotAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(old), SqlAccountWorldHandoffFailure.SessionRetired);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await AssertSqlHandoffRedeemedAsync(f, await f.RedeemAsync(fresh));
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffServerUtcExpiryRefusesAndReleasesWithoutExplicitOfferDisposal() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync(f.CreateIssuer(TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(1), offer.ExpiresAtUtc - offer.IssuedAtUtc);
        var before = await f.SnapshotAsync();
        await f.Database.WaitForSqlHandoffExpiryAsync(offer.Token);
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.Expired);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.WaitForSqlHandoffIssuerReleaseAsync(offer.AccountId);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(offer.AccountId);
        await offer.DisposeAsync();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffKilledIssuerCannotPublishWorldOwnershipOrConsumeTicket() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var issuerId = await f.Database.ObserveAuthenticationHolderAsync(offer.AccountId);
        Assert.NotNull(issuerId);
        var before = await f.SnapshotAsync();
        await f.Database.KillOwnedAuthenticationCustodyConnectionAsync(offer.AccountId);
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.SessionRetired);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        var observed = await Record.ExceptionAsync(async () => { await offer.DisposeAsync(); });
        if (observed is AggregateException) ExceptionDispatchInfo.Capture(observed).Throw();
        if (observed is not null) Assert.IsType<MySqlException>(observed);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(offer.AccountId);
        await f.Database.AssertSqlHandoffOriginalIssuerSessionGoneAsync(issuerId!.Value);
        var fresh = await f.RequireFreshAuthenticationOwnerAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await fresh.VerifyAsync());
        Assert.Equal(offer.AccountId, fresh.AccountId); Assert.Equal(offer.CharacterUid, fresh.CharacterUid);
        Assert.True(f.UnregisterJoinedKilledOffer(offer));
        if (observed is MySqlException expected)
            Console.WriteLine("Owned SQL handoff KILL: joined disposal retained the expected MySqlException; number=" + expected.Number);
        else Console.WriteLine("Owned SQL handoff KILL: joined disposal completed without a provider exception.");
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
    });
}
