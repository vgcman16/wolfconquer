using System.Data;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationClosedTargetFaultPreservesHealthyLocalOwner() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        var holder = await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId);
        Assert.NotNull(holder);
        var target = await f.OpenDomainAsync();
        await target.CloseAsync();
        Assert.Equal(ConnectionState.Closed, target.State);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.VerifyDomainAsync(target, null, default).AsTask());
        Assert.Equal("sqlAuthenticationTargetNotOpen", failure.Message);

        var healthyTarget = await f.OpenDomainAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyDomainAsync(healthyTarget, null, default));
        await AssertHealthyAuthenticationAfterTargetFaultAsync(f, owner, holder, before);
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationMismatchedTargetTransactionFaultPreservesHealthyLocalOwner()
    {
        foreach (bool ignore in new[] { false, true })
            await RunAuthenticationCustodyAsync(async f =>
            {
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                var owner = await f.RequireOwnerAsync();
                var holder = await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId);
                Assert.NotNull(holder);
                var target = await f.OpenDomainAsync(ignoreCommandTransaction: ignore);
                var transactionConnection = await f.OpenDomainAsync(ignoreCommandTransaction: ignore);
                Assert.NotSame(target, transactionConnection);
                // Both provider settings use actual guarded fixture connections;
                // application affinity must not depend on the provider's guard.
                Assert.Equal(ignore, new MySqlConnectionStringBuilder(target.ConnectionString).IgnoreCommandTransaction);
                Assert.Equal(ignore, new MySqlConnectionStringBuilder(transactionConnection.ConnectionString).IgnoreCommandTransaction);
                var transaction = await f.BeginDomainTransactionAsync(transactionConnection);
                Assert.Same(transactionConnection, transaction.Connection);
                Assert.Equal(SqlAccountAuthenticationFailure.None,
                    await owner.VerifyDomainAsync(transactionConnection, transaction, default));

                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    owner.VerifyDomainAsync(target, transaction, default).AsTask());
                Assert.Equal("sqlAuthenticationTargetTransactionMismatch", failure.Message);

                Assert.Same(transactionConnection, transaction.Connection);
                Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyDomainAsync(target, null, default));
                Assert.Equal(SqlAccountAuthenticationFailure.None,
                    await owner.VerifyDomainAsync(transactionConnection, transaction, default));
                await AssertHealthyAuthenticationAfterTargetFaultAsync(f, owner, holder, before);
            });
    }

    private static async Task AssertHealthyAuthenticationAfterTargetFaultAsync(AuthenticationCustodyFixture f,
        SqlAccountAuthenticationCustody owner, ulong? originalHolder, string[] before)
    {
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        var holder = await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId);
        Assert.NotNull(holder);
        Assert.Equal(originalHolder, holder);
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.SessionConflict);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    }
}
