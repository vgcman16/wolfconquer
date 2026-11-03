using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationCustodyRetainsCanonicalIdentityWithoutWritingRows() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        Assert.Equal((long)AuthenticationCustodyFixture.FirstAccount, owner.AccountId);
        Assert.Equal(OwnedModernSqlAuthDatabase.FirstUsername, owner.Username);
        Assert.Equal(AuthenticationCustodyFixture.FirstAccount + 1000, owner.CharacterUid);
        Assert.NotNull(await f.Database.ObserveAuthenticationHolderAsync(owner.AccountId));
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationAllowsUncreatedCharacterWithoutInventingCharacterAuthority() => RunAuthenticationCustodyAsync(async f =>
    {
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.EntityId, null);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        Assert.Equal(0u, owner.CharacterUid);
        Assert.Equal((long)AuthenticationCustodyFixture.FirstAccount, owner.AccountId);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationUsesStrictPasswordAndLeavesNoRefusedClaims()
    {
        foreach (string password in new[] { "wrong_owned_password", "FIXTURE_SQL_PW", OwnedModernSqlAuthDatabase.SyntheticPassword + "A1" })
            await RunAuthenticationCustodyAsync(async f =>
            {
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                AssertAuthenticationRefused(await f.AcquireAsync(password: password), SqlAccountAuthenticationFailure.WrongPassword);
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
                await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
                Assert.Equal(SqlAccountAuthenticationFailure.None, await (await f.RequireOwnerAsync()).VerifyAsync());
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationMissingUsernameNeverIssuesCustody()
    {
        foreach (string username in new[] { "", " \t", "missing-owned-account" })
            await RunAuthenticationCustodyAsync(async f =>
            {
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                AssertAuthenticationRefused(await f.AcquireAsync(username), SqlAccountAuthenticationFailure.MissingUsername);
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
                await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationRejectsDuplicateUsernameBeforeMatchingPassword() => RunAuthenticationCustodyAsync(async f =>
    {
        await f.Database.AddAuthenticationDuplicateAsync(AuthenticationCustodyFixture.DuplicateAccount);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.AmbiguousAccount);
        AssertAuthenticationRefused(await f.AcquireAsync(password: "wrong_owned_password"), SqlAccountAuthenticationFailure.AmbiguousAccount);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount, AuthenticationCustodyFixture.DuplicateAccount);
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationRejectsBannedAndEveryNonzeroOnlineRow()
    {
        foreach (var denial in new[]
        {
            (AccountColumn.BannedId, -1L, SqlAccountAuthenticationFailure.Banned),
            (AccountColumn.BannedId, 0L, SqlAccountAuthenticationFailure.Banned),
            (AccountColumn.Online, 1L, SqlAccountAuthenticationFailure.AlreadyOnline),
            (AccountColumn.Online, -7L, SqlAccountAuthenticationFailure.AlreadyOnline)
        })
            await RunAuthenticationCustodyAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, denial.Item1, denial.Item2);
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                AssertAuthenticationRefused(await f.AcquireAsync(), denial.Item3);
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
                await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationPreservesDatabaseUsernameCollationAndCanonicalRowName() => RunAuthenticationCustodyAsync(async f =>
    {
        string requestName = OwnedModernSqlAuthDatabase.FirstUsername.ToUpperInvariant();
        var selected = await f.Database.CreateRepository().FindByUsernameAsync(requestName);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var acquired = await f.AcquireAsync(requestName);
        if (selected.Count == 0)
            AssertAuthenticationRefused(acquired, SqlAccountAuthenticationFailure.MissingUsername);
        else
        {
            var actual = Assert.Single(selected);
            Assert.True(acquired.IsAcquired);
            Assert.Equal(SqlAccountAuthenticationFailure.None, acquired.Failure);
            var owner = Assert.IsType<SqlAccountAuthenticationCustody>(acquired.Custody);
            Assert.Equal(actual.Username, owner.Username);
            Assert.Equal(OwnedModernSqlAuthDatabase.FirstUsername, owner.Username);
            Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        }
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });
}
