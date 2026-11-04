using System.Reflection;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffKilledIssuerOfferReportsJoinedMonitorFailureOnceAndDisposesOnRetry() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var issuerId = await f.Database.ObserveAuthenticationHolderAsync(offer.AccountId);
        Assert.NotNull(issuerId);
        var before = await f.SnapshotAsync();
        await f.Database.KillOwnedAuthenticationCustodyConnectionAsync(offer.AccountId);

        // Observe the actual existing monitor task, never construct or replace
        // an owner, task outcome, callback or provider reply. Awaiting it forces
        // the real KILL fault to settle before the first disposal attempts cleanup.
        var field = typeof(SqlAccountWorldHandoffOffer).GetField("monitor", BindingFlags.Instance | BindingFlags.NonPublic);
        var monitor = Assert.IsAssignableFrom<Task>(field?.GetValue(offer));
        var observed = await Record.ExceptionAsync(async () =>
            await monitor.WaitAsync(TimeSpan.FromSeconds(10)));
        var expected = Assert.IsType<MySqlException>(observed);
        var firstDisposal = await Record.ExceptionAsync(async () => await offer.DisposeAsync());
        Assert.Same(expected, firstDisposal);

        // A previously reported historical monitor failure cannot make every
        // subsequent physical-close retry fail after the actual owner is gone.
        await offer.DisposeAsync();
        await f.Database.AssertAuthenticationClaimsReleasedAsync(offer.AccountId);
        await f.Database.AssertSqlHandoffOriginalIssuerSessionGoneAsync(issuerId!.Value);
        var fresh = await f.RequireFreshAuthenticationOwnerAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await fresh.VerifyAsync());
        Assert.Equal(offer.AccountId, fresh.AccountId);
        Assert.Equal(offer.CharacterUid, fresh.CharacterUid);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        // Keep the offer registered: fixture cleanup must also dispose it cleanly.
    });
}
