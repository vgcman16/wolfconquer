using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlLoginServiceStrictAndLegacyValidRowsRemainUsableAndUnchanged() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var service = new AccountLoginService(f.Database.CreateRepository());
        var exact = new AccountLoginRequest(OwnedModernSqlAuthDatabase.FirstUsername, OwnedModernSqlAuthDatabase.SyntheticPassword);
        AssertOwnedSqlLoginServiceSuccess(await service.ValidateAsync(exact), AccountPasswordMatchKind.Exact);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        AssertOwnedSqlLoginServiceSuccess(await service.ValidateLegacyAsync(exact), AccountPasswordMatchKind.Exact);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        var legacy = exact with { Password = exact.Password + "A1" };
        AssertOwnedSqlLoginServiceSuccess(await service.ValidateLegacyAsync(legacy), AccountPasswordMatchKind.LegacyTrailingNoise);
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlLoginServiceRequiredNullBannedIdCannotReturnStrictOrLegacyResult() => RunAuthenticationCustodyAsync(f =>
        AssertOwnedSqlLoginServiceNullPermissionAsync(f, AccountColumn.BannedId, 2L,
            "sqlAccountAuthenticationStoredIntegerInvalid"));

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlLoginServiceRequiredNullOnlineCannotReturnStrictOrLegacyResult() => RunAuthenticationCustodyAsync(f =>
        AssertOwnedSqlLoginServiceNullPermissionAsync(f, AccountColumn.Online, 0L,
            "sqlAccountAuthenticationStoredOnlineInvalid"));

    private static async Task AssertOwnedSqlLoginServiceNullPermissionAsync(AuthenticationCustodyFixture f,
        AccountColumn column, long restoredValue, string expectedMessage)
    {
        var original = await f.Database.SnapshotAuthenticationRowsAsync();
        var service = new AccountLoginService(f.Database.CreateRepository());
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, column, null);
        var malformed = await f.Database.SnapshotAuthenticationRowsAsync();
        foreach (var mode in new[] { (Legacy: false, TrailingNoise: false), (Legacy: true, TrailingNoise: false), (Legacy: true, TrailingNoise: true) })
        {
            string password = OwnedModernSqlAuthDatabase.SyntheticPassword + (mode.TrailingNoise ? "A1" : "");
            var request = new AccountLoginRequest(OwnedModernSqlAuthDatabase.FirstUsername, password);
            AccountLoginResult? returned = null;
            var failure = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                returned = mode.Legacy ? await service.ValidateLegacyAsync(request) : await service.ValidateAsync(request);
            });
            Assert.Equal(expectedMessage, failure.Message);
            Assert.Null(returned);
            Assert.Equal(malformed, await f.Database.SnapshotAuthenticationRowsAsync());
        }
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);

        // Restore only the explicitly mutated fixture cell. The same actual
        // service must succeed again; unavailable SQL is never a valid refusal.
        await f.Database.SetAuthenticationAccountColumnAsync(AuthenticationCustodyFixture.FirstAccount, column, restoredValue);
        var valid = new AccountLoginRequest(OwnedModernSqlAuthDatabase.FirstUsername, OwnedModernSqlAuthDatabase.SyntheticPassword);
        AssertOwnedSqlLoginServiceSuccess(await service.ValidateAsync(valid), AccountPasswordMatchKind.Exact);
        Assert.Equal(original, await f.Database.SnapshotAuthenticationRowsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(AuthenticationCustodyFixture.FirstAccount);
    }

    private static void AssertOwnedSqlLoginServiceSuccess(AccountLoginResult result, AccountPasswordMatchKind matchKind)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(AccountLoginStatus.Success, result.Status);
        Assert.Equal(matchKind, result.PasswordMatchKind);
        Assert.Null(result.PasswordDiagnostic);
        var account = Assert.IsType<AccountRecord>(result.Account);
        Assert.Equal((long)AuthenticationCustodyFixture.FirstAccount, account.Id);
        Assert.Equal(OwnedModernSqlAuthDatabase.FirstUsername, account.Username);
        Assert.Equal((long)AuthenticationCustodyFixture.FirstAccount + 1000, account.EntityId);
        Assert.Equal(AccountLoginService.AllowedBannedId, account.BannedId);
        Assert.False(account.IsOnline);
    }
}
