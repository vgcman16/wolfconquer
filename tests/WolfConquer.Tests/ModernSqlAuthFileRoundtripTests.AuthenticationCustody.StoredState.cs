using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationNullPermissionsAreInvalidAndNeverDefaultToAllowed()
    {
        foreach (var column in new[] { AccountColumn.BannedId, AccountColumn.Online })
            await RunAuthenticationCustodyAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, column, null);
                await AssertInvalidAuthenticationRowsAsync(f);
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationRejectsNonpositiveAccountAndOutOfRangeCharacterIdentity()
    {
        foreach (var malformed in new[]
        {
            (AccountColumn.Id, 0L), (AccountColumn.Id, -1L),
            (AccountColumn.EntityId, -1L), (AccountColumn.EntityId, (long)uint.MaxValue + 1)
        })
            await RunAuthenticationCustodyAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, malformed.Item1, malformed.Item2);
                await AssertInvalidAuthenticationRowsAsync(f);
            });
    }

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationDoesNotCoerceNumericTextIntoIdentityOrPermissions()
    {
        foreach (var column in new[] { AccountColumn.Id, AccountColumn.EntityId, AccountColumn.BannedId, AccountColumn.Online })
            await RunAuthenticationCustodyAsync(async f =>
            {
                await f.Database.MakeAuthenticationNumericColumnTextAsync(column);
                await AssertInvalidAuthenticationRowsAsync(f);
            });
    }

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationRejectsNullPasswordAndCompleteMalformedCandidateTail() => RunAuthenticationCustodyAsync(async f =>
    {
        await f.Database.AllowNullAuthenticationCredentialsAsync();
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.Password, null);
        await AssertInvalidAuthenticationRowsAsync(f);
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount,
            AccountColumn.Password, OwnedModernSqlAuthDatabase.SyntheticPassword);
        await f.Database.AddAuthenticationDuplicateAsync(AuthenticationCustodyFixture.DuplicateAccount);
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.DuplicateAccount, AccountColumn.EntityId, -1L);
        // The first matching password and a valid first row cannot hide a bad
        // later SQL row or publish a partial candidate list.
        await AssertInvalidAuthenticationRowsAsync(f);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationNullOrWhitespaceStoredUsernameCannotAuthorize() => RunAuthenticationCustodyAsync(async f =>
    {
        await f.Database.AllowNullAuthenticationCredentialsAsync();
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.Username, "   ");
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await f.Database.CreateRepository().FindByUsernameAsync("   "));
        AssertAuthenticationRefused(await f.AcquireAsync("   "), SqlAccountAuthenticationFailure.MissingUsername);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.Username, null);
        before = await f.Database.SnapshotAuthenticationRowsAsync();
        // NULL cannot match the parameterized username predicate. This is a
        // missing candidate, not invented evidence that SQL returned a NULL name.
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.MissingUsername);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
    });

    [ModernSqlAuthFixtureFact]
    public async Task ActualSqlAuthenticationAcceptsExactEmptyPasswordAndFullCharacterUidBounds()
    {
        foreach (long characterUid in new[] { 0L, (long)uint.MaxValue })
            await RunAuthenticationCustodyAsync(async f =>
            {
                await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.Password, "");
                await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, AccountColumn.EntityId, characterUid);
                var before = await f.Database.SnapshotAuthenticationRowsAsync();
                AssertAuthenticationRefused(await f.AcquireAsync(password: null!), SqlAccountAuthenticationFailure.WrongPassword);
                await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
                var owner = await f.RequireOwnerAsync(password: "");
                Assert.Equal((uint)characterUid, owner.CharacterUid);
                Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
                Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
            });
    }

    private static async Task AssertInvalidAuthenticationRowsAsync(AuthenticationCustodyFixture f)
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername));
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.InvalidAccountState);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount, AuthenticationCustodyFixture.DuplicateAccount);
    }
}
