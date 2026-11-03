using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationPasswordChangeRetiresOwnerAndRequiresCurrentCredential() => RunAuthenticationCustodyAsync(async f =>
    {
        var owner = await f.RequireOwnerAsync();
        const string changedPassword = "changed_owned_fixture_password";
        await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, AccountColumn.Password, changedPassword);
        await AssertAuthenticationRetiredAsync(f, owner, SqlAccountAuthenticationFailure.IdentityConflict);
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.WrongPassword);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await (await f.RequireOwnerAsync(password: changedPassword)).VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationIdentityChangesRetireWithoutRebindingOldOwner()
    {
        foreach (var change in new[]
        {
            (AccountColumn.Username, (object)"renamed-owned-account"),
            (AccountColumn.EntityId, (object)9001L),
            (AccountColumn.Id, (object)504L)
        })
            await RunAuthenticationCustodyAsync(async f =>
            {
                var owner = await f.RequireOwnerAsync();
                long originalAccountId = owner.AccountId;
                uint originalCharacterUid = owner.CharacterUid;
                string originalUsername = owner.Username;
                await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, change.Item1, change.Item2);
                await AssertAuthenticationRetiredAsync(f, owner, SqlAccountAuthenticationFailure.IdentityConflict);
                Assert.Equal(originalAccountId, owner.AccountId);
                Assert.Equal(originalCharacterUid, owner.CharacterUid);
                Assert.Equal(originalUsername, owner.Username);
                string username = change.Item1 == AccountColumn.Username ? (string)change.Item2 : originalUsername;
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                var next = await f.RequireOwnerAsync(username);
                Assert.NotSame(owner, next);
                Assert.Equal(change.Item1 == AccountColumn.Id ? (long)change.Item2 : originalAccountId, next.AccountId);
                Assert.Equal(change.Item1 == AccountColumn.EntityId ? (uint)(long)change.Item2 : originalCharacterUid, next.CharacterUid);
                Assert.Equal(username, next.Username);
                Assert.Equal(SqlAccountAuthenticationFailure.None, await next.VerifyAsync());
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationPermissionChangesRetireBeforeLaterUse()
    {
        foreach (var change in new[]
        {
            (AccountColumn.BannedId, 1L, SqlAccountAuthenticationFailure.Banned),
            (AccountColumn.Online, 1L, SqlAccountAuthenticationFailure.AlreadyOnline)
        })
            await RunAuthenticationCustodyAsync(async f =>
            {
                var owner = await f.RequireOwnerAsync();
                await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, change.Item1, change.Item2);
                await AssertAuthenticationRetiredAsync(f, owner, SqlAccountAuthenticationFailure.IdentityConflict);
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                AssertAuthenticationRefused(await f.AcquireAsync(), change.Item3);
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationMalformedChangedRowRetiresInsteadOfKeepingPriorPermissions() => RunAuthenticationCustodyAsync(async f =>
    {
        var owner = await f.RequireOwnerAsync();
        await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, AccountColumn.BannedId, null);
        await AssertAuthenticationRetiredAsync(f, owner, SqlAccountAuthenticationFailure.InvalidAccountState);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.InvalidAccountState);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationDeletedOrDuplicatedRowRetiresAndReleasesClaims() => RunAuthenticationCustodyAsync(async f =>
    {
        var owner = await f.RequireOwnerAsync();
        await f.Database.AddAuthenticationDuplicateAsync(AuthenticationCustodyFixture.DuplicateAccount);
        await AssertAuthenticationRetiredAsync(f, owner, SqlAccountAuthenticationFailure.IdentityConflict);
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.AmbiguousAccount);
        await f.Database.RemoveOwnedAuthenticationAccountAsync(AuthenticationCustodyFixture.DuplicateAccount);
        var replacement = await f.ReacquireClosedOwnerAsync();
        await f.Database.RemoveOwnedAuthenticationAccountAsync(replacement.AccountId);
        await AssertAuthenticationRetiredAsync(f, replacement, SqlAccountAuthenticationFailure.IdentityConflict);
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.MissingUsername);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    private static async Task AssertAuthenticationRetiredAsync(AuthenticationCustodyFixture f,
        SqlAccountAuthenticationCustody owner, SqlAccountAuthenticationFailure failure)
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        Assert.Equal(failure, await owner.VerifyAsync());
        Assert.Equal(SqlAccountAuthenticationFailure.SessionRetired, await owner.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
        await owner.DisposeAsync();
    }
}
