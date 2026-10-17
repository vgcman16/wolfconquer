using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed class AccountLoginServiceTests
{
    [Fact]
    public async Task ValidateReturnsMissingUsernameWhenNoRowsExist()
    {
        var service = new AccountLoginService(new FakeAccountRepository());

        var result = await service.ValidateAsync(new AccountLoginRequest("missing", "password"));

        Assert.Equal(AccountLoginStatus.MissingUsername, result.Status);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task ValidateReturnsWrongPasswordWhenNoStoredPasswordMatches()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(1, "demon1", "stored")));

        var result = await service.ValidateAsync(new AccountLoginRequest("demon1", "wrong"));

        Assert.Equal(AccountLoginStatus.WrongPassword, result.Status);
        Assert.Null(result.Account);
        Assert.NotNull(result.PasswordDiagnostic);
        Assert.Equal(5, result.PasswordDiagnostic.RequestLength);
        Assert.Equal(1, result.PasswordDiagnostic.CandidateCount);
        Assert.Equal(6, result.PasswordDiagnostic.BestStoredLength);
        Assert.Equal(0, result.PasswordDiagnostic.BestCommonPrefixLength);
        Assert.Equal(-1, result.PasswordDiagnostic.BestLengthDelta);
        Assert.Equal("none", result.PasswordDiagnostic.TailShape);
        Assert.Equal(0, result.PasswordDiagnostic.ExactPasswordOwnerCount);
        Assert.Null(result.PasswordDiagnostic.FirstExactPasswordOwnerId);
        Assert.Null(result.PasswordDiagnostic.FirstExactPasswordOwnerUsername);
    }

    [Fact]
    public async Task ValidateWrongPasswordReportsExactPasswordOwnerWithoutExposingPassword()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(1, "user03", "stored"),
            Account(2, "user02", "wrong")));

        var result = await service.ValidateAsync(new AccountLoginRequest("user03", "wrong"));

        Assert.Equal(AccountLoginStatus.WrongPassword, result.Status);
        Assert.NotNull(result.PasswordDiagnostic);
        Assert.Equal(1, result.PasswordDiagnostic.ExactPasswordOwnerCount);
        Assert.Equal(2, result.PasswordDiagnostic.FirstExactPasswordOwnerId);
        Assert.Equal("user02", result.PasswordDiagnostic.FirstExactPasswordOwnerUsername);
    }

    [Fact]
    public async Task ValidateLegacySavedPasswordOwnerRebindAcceptsUniqueTargetWhenSubmittedPasswordBelongsToOwner()
    {
        var target = Account(1296474, "user03", "fixture01-1w");
        var service = new AccountLoginService(new FakeAccountRepository(
            target,
            Account(1296471, "guest", "fixture2-pw")));

        var result = await service.ValidateLegacySavedPasswordOwnerRebindAsync(
            new AccountLoginRequest("user03", "fixture2-pw"),
            "guest");

        Assert.Equal(AccountLoginStatus.Success, result.Status);
        Assert.Equal(target, result.Account);
        Assert.Equal(AccountPasswordMatchKind.LegacySavedPasswordOwnerRebind, result.PasswordMatchKind);
    }

    [Fact]
    public async Task ValidateLegacySavedPasswordOwnerRebindRejectsWhenSubmittedPasswordOwnerDoesNotMatch()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(1296474, "user03", "fixture01-1w"),
            Account(1296471, "guest", "fixture2-pw")));

        var result = await service.ValidateLegacySavedPasswordOwnerRebindAsync(
            new AccountLoginRequest("user03", "fixture2-pw"),
            "other");

        Assert.Equal(AccountLoginStatus.WrongPassword, result.Status);
        Assert.Null(result.Account);
        Assert.NotNull(result.PasswordDiagnostic);
        Assert.Equal(1, result.PasswordDiagnostic.ExactPasswordOwnerCount);
        Assert.Equal("guest", result.PasswordDiagnostic.FirstExactPasswordOwnerUsername);
    }

    [Fact]
    public async Task ValidateKeepsInternalPasswordMatchingStrict()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(1, "demon1", "stored")));

        var result = await service.ValidateAsync(new AccountLoginRequest("demon1", "storedsS"));

        Assert.Equal(AccountLoginStatus.WrongPassword, result.Status);
        Assert.Equal(AccountPasswordMatchKind.None, result.PasswordMatchKind);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task ValidateLegacyAcceptsTwoCharacterTrailingPasswordNoise()
    {
        var account = Account(1, "demon1", "stored");
        var service = new AccountLoginService(new FakeAccountRepository(account));

        var result = await service.ValidateLegacyAsync(new AccountLoginRequest("demon1", "storedsS"));

        Assert.Equal(AccountLoginStatus.Success, result.Status);
        Assert.Equal(AccountPasswordMatchKind.LegacyTrailingNoise, result.PasswordMatchKind);
        Assert.Equal(account, result.Account);
    }

    [Fact]
    public async Task ValidateLegacyRejectsDifferentTrailingPasswordLengths()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(1, "demon1", "stored")));

        var result = await service.ValidateLegacyAsync(new AccountLoginRequest("demon1", "storedxyz"));

        Assert.Equal(AccountLoginStatus.WrongPassword, result.Status);
        Assert.Equal(AccountPasswordMatchKind.None, result.PasswordMatchKind);
        Assert.NotNull(result.PasswordDiagnostic);
        Assert.Equal(9, result.PasswordDiagnostic.RequestLength);
        Assert.Equal(6, result.PasswordDiagnostic.BestStoredLength);
        Assert.Equal(6, result.PasswordDiagnostic.BestCommonPrefixLength);
        Assert.Equal(3, result.PasswordDiagnostic.BestLengthDelta);
        Assert.Equal("LLL", result.PasswordDiagnostic.TailShape);
    }

    [Fact]
    public async Task ValidateSelectsMatchingPasswordFromDuplicateUsernameRows()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(2, "2", ""),
            Account(1293600, "2", "2"),
            Account(1294637, "2", "10203040")));

        var result = await service.ValidateAsync(new AccountLoginRequest("2", "2"));

        Assert.Equal(AccountLoginStatus.Success, result.Status);
        Assert.Equal(1293600, result.Account?.Id);
    }

    [Fact]
    public async Task ValidateReturnsBannedWhenMatchingAccountIsNotAllowed()
    {
        var account = Account(10, "banned", "pass", bannedId: 1);
        var service = new AccountLoginService(new FakeAccountRepository(account));

        var result = await service.ValidateAsync(new AccountLoginRequest("banned", "pass"));

        Assert.Equal(AccountLoginStatus.Banned, result.Status);
        Assert.Equal(account, result.Account);
    }

    [Fact]
    public async Task ValidateReturnsAlreadyOnlineWhenMatchingAccountIsOnline()
    {
        var account = Account(10, "online", "pass", isOnline: true);
        var service = new AccountLoginService(new FakeAccountRepository(account));

        var result = await service.ValidateAsync(new AccountLoginRequest("online", "pass"));

        Assert.Equal(AccountLoginStatus.AlreadyOnline, result.Status);
        Assert.Equal(account, result.Account);
    }

    [Fact]
    public async Task ValidateReturnsAmbiguousWhenMultipleStoredPasswordsMatch()
    {
        var service = new AccountLoginService(new FakeAccountRepository(
            Account(10, "dupe", "pass"),
            Account(11, "dupe", "pass")));

        var result = await service.ValidateAsync(new AccountLoginRequest("dupe", "pass"));

        Assert.Equal(AccountLoginStatus.AmbiguousAccount, result.Status);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task ValidateReturnsSuccessForAllowedOfflineMatchingAccount()
    {
        var account = Account(1296473, "user02", "fixture-pw");
        var service = new AccountLoginService(new FakeAccountRepository(account));

        var result = await service.ValidateAsync(new AccountLoginRequest("user02", "fixture-pw"));

        Assert.True(result.IsSuccess);
        Assert.Equal(account, result.Account);
        Assert.Equal(AccountPasswordMatchKind.Exact, result.PasswordMatchKind);
    }

    private static AccountRecord Account(
        long id,
        string username,
        string password,
        long entityId = 0,
        long bannedId = AccountLoginService.AllowedBannedId,
        bool isOnline = false)
    {
        return new AccountRecord(id, username, password, entityId, bannedId, isOnline);
    }

    private sealed class FakeAccountRepository(params AccountRecord[] accounts) :
        IAccountRepository,
        IAccountPasswordDiagnosticRepository
    {
        public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountRecord> matches = accounts
                .Where(account => string.Equals(account.Username, username, StringComparison.Ordinal))
                .ToArray();
            return ValueTask.FromResult(matches);
        }

        public ValueTask<IReadOnlyList<AccountPasswordOwnerDiagnostic>> FindByPasswordAsync(
            string password,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountPasswordOwnerDiagnostic> matches = accounts
                .Where(account => string.Equals(account.Password, password, StringComparison.Ordinal))
                .Select(account => new AccountPasswordOwnerDiagnostic(account.Id, account.Username, account.Password.Length))
                .ToArray();
            return ValueTask.FromResult(matches);
        }
    }
}
