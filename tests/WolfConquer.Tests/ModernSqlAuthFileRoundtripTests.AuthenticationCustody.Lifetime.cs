using System.Runtime.ExceptionServices;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationCompetesForOneAccountAndKeepsOtherAccountIndependent() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var left = f.AcquireAsync();
        var right = f.AcquireAsync();
        // Task.WhenAll awaits both actual started acquisitions even if one
        // throws; the fixture registers any issued owner before task completion.
        var attempts = await Task.WhenAll(left, right);
        var acquired = Assert.Single(attempts, attempt => attempt.IsAcquired);
        var owner = Assert.IsType<SqlAccountAuthenticationCustody>(acquired.Custody);
        Assert.Equal(SqlAccountAuthenticationFailure.None, acquired.Failure);
        AssertAuthenticationRefused(Assert.Single(attempts, attempt => !attempt.IsAcquired), SqlAccountAuthenticationFailure.SessionConflict);
        var peer = await f.RequireOwnerAsync(OwnedModernSqlAuthDatabase.SecondUsername);
        var holder = await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId);
        var peerHolder = await f.Database.ObserveAuthenticationHolderAsync(peer.AccountId);
        Assert.NotNull(holder); Assert.NotNull(peerHolder); Assert.NotEqual(holder, peerHolder);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        Assert.Equal(SqlAccountAuthenticationFailure.None, await peer.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationPrecancelDoesNotIssueOrRetainCustody() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        SqlAccountAuthenticationAcquireResult? returned = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            returned = await f.AcquireAsync(token: cancelled.Token);
        });
        Assert.Null(returned);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await (await f.RequireOwnerAsync()).VerifyAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationDisposeRetiresAndReleasesForFreshOwner() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        ulong? oldHolder = await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId);
        Assert.NotNull(oldHolder);
        await owner.DisposeAsync();
        await owner.DisposeAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, await owner.VerifyAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
        var reopened = await f.ReacquireClosedOwnerAsync();
        Assert.NotSame(owner, reopened);
        Assert.Equal(owner.AccountId, reopened.AccountId);
        Assert.NotNull(await f.Database.ObserveAuthenticationHolderAsync(reopened.AccountId));
        Assert.Equal(SqlAccountAuthenticationFailure.None, await reopened.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationStartedVerifyAndDisposeCompleteBeforeReacquisition() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        var verify = owner.VerifyAsync().AsTask();
        var dispose = owner.DisposeAsync().AsTask();
        await Task.WhenAll(verify, dispose);
        Assert.Contains(await verify, new[] { SqlAccountAuthenticationFailure.None, SqlAccountAuthenticationFailure.SessionRetired });
        Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, await owner.VerifyAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await (await f.ReacquireClosedOwnerAsync()).VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationKilledConnectionRetiresWithoutChangingAccounts() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        await f.Database.KillOwnedAuthenticationCustodyConnectionAsync(owner.AccountId);
        SqlAccountAuthenticationFailure? first = null;
        var failure = await Record.ExceptionAsync(async () => { first = await owner.VerifyAsync(); });
        if (failure is null) Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, first);
        else AssertAuthenticationProviderFailure(failure);
        Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, await owner.VerifyAsync());
        await owner.DisposeAsync();
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await (await f.ReacquireClosedOwnerAsync()).VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationProviderFaultsCannotBecomeCredentialRefusals()
    {
        await RunAuthenticationCustodyAsync(async f =>
        {
            await f.Database.RemoveOwnedAuthenticationTableAsync();
            SqlAccountAuthenticationAcquireResult? returned = null;
            var failure = await Record.ExceptionAsync(async () => { returned = await f.AcquireAsync(); });
            Assert.NotNull(failure); AssertAuthenticationProviderFailure(failure);
            Assert.Null(returned);
            await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
        });
        await RunAuthenticationCustodyAsync(async f =>
        {
            var owner = await f.RequireOwnerAsync();
            await f.Database.RemoveOwnedAuthenticationTableAsync();
            var failure = await Record.ExceptionAsync(async () => { await owner.VerifyAsync(); });
            Assert.NotNull(failure); AssertAuthenticationProviderFailure(failure);
            Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, await owner.VerifyAsync());
            await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
            await owner.DisposeAsync();
        });
    }

    private static void AssertAuthenticationProviderFailure(Exception failure)
    {
        // The expected fault is a real provider error. A simultaneous cleanup
        // failure must remain a test failure with every original inner error.
        if (failure is AggregateException) ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.IsAssignableFrom<MySqlException>(failure);
    }
}
