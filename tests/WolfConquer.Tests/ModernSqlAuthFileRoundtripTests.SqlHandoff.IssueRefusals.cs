using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffMissingAndStrictWrongCredentialsCannotInsertTicketsOrChangeWorld() => RunSqlHandoffAsync(async f =>
    {
        var before = await f.SnapshotAsync();
        AssertSqlHandoffIssueRefused(await f.IssueAsync(username: "missing-owned-account"), SqlAccountWorldHandoffFailure.MissingUsername);
        foreach (string password in new[] { "wrong-owned-password", "FIXTURE_SQL_PW", OwnedModernSqlAuthDatabase.SyntheticPassword + "A1" })
        {
            AssertSqlHandoffIssueRefused(await f.IssueAsync(password: password), SqlAccountWorldHandoffFailure.WrongPassword);
            await AssertSqlHandoffRowsUnchangedAsync(f, before);
        }
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
        await f.RequireOfferAsync();
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffDuplicateUsernameCannotSelectTheMatchingPasswordOwner() => RunSqlHandoffAsync(async f =>
    {
        await f.Database.AddAuthenticationDuplicateAsync(503);
        var before = await f.SnapshotAsync();
        AssertSqlHandoffIssueRefused(await f.IssueAsync(), SqlAccountWorldHandoffFailure.AmbiguousAccount);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount, 503);
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffRequiredNullPermissionsCannotIssueAnOffer()
    {
        foreach (var column in new[] { AccountColumn.BannedId, AccountColumn.Online })
            await RunSqlHandoffAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(SqlWorldStateTestData.FirstAccount, column, null);
                var before = await f.SnapshotAsync();
                AssertSqlHandoffIssueRefused(await f.IssueAsync(), SqlAccountWorldHandoffFailure.InvalidAccountState);
                await AssertSqlHandoffRowsUnchangedAsync(f, before);
                await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlHandoffBannedOrOnlineRowsCannotIssueAnOffer()
    {
        foreach (var denial in new[]
        {
            (AccountColumn.BannedId, 1L, SqlAccountWorldHandoffFailure.Banned),
            (AccountColumn.Online, -7L, SqlAccountWorldHandoffFailure.AlreadyOnline)
        })
            await RunSqlHandoffAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(SqlWorldStateTestData.FirstAccount, denial.Item1, denial.Item2);
                var before = await f.SnapshotAsync();
                AssertSqlHandoffIssueRefused(await f.IssueAsync(), denial.Item3);
                await AssertSqlHandoffRowsUnchangedAsync(f, before);
                await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffRequiresMatchingCurrentWorldCharacterUid() => RunSqlHandoffAsync(async f =>
    {
        await f.Database.SetAuthenticationAccountColumnAsync(SqlWorldStateTestData.FirstAccount, AccountColumn.EntityId, 9003L);
        var before = await f.SnapshotAsync();
        AssertSqlHandoffIssueRefused(await f.IssueAsync(), SqlAccountWorldHandoffFailure.IdentityConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffMissingWorldBundleCannotBecomeAnAuthenticatedWorldOwner() => RunSqlHandoffAsync(async f =>
    {
        var before = await f.SnapshotAsync();
        AssertSqlHandoffIssueRefused(await f.IssueAsync(), SqlAccountWorldHandoffFailure.WorldUnavailable);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
    }, seedWorld: false);

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffCorruptRequiredWorldImageIsAnOperationalInvalidDataFailure() => RunSqlHandoffAsync(async f =>
    {
        await f.Database.CorruptSqlHandoffWorldSpellImageAsync(SqlWorldStateTestData.FirstAccount);
        var before = await f.SnapshotAsync();
        SqlAccountWorldHandoffIssueResult? returned = null;
        await Assert.ThrowsAsync<InvalidDataException>(async () => { returned = await f.IssueAsync(); });
        Assert.Null(returned);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        await f.Database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount);
    });
}
