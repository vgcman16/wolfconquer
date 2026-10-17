namespace WolfConquer.Core.Accounts;

public sealed class AccountLoginService
{
    public const long AllowedBannedId = 2;

    private readonly IAccountRepository _accounts;

    public AccountLoginService(IAccountRepository accounts)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    public async ValueTask<AccountLoginResult> ValidateAsync(
        AccountLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(request, AccountPasswordCompatibility.Strict, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<AccountLoginResult> ValidateLegacyAsync(
        AccountLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(request, AccountPasswordCompatibility.LegacyTrailingNoise, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<AccountLoginResult> ValidateLegacySavedPasswordOwnerRebindAsync(
        AccountLoginRequest request,
        string ownerUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ownerUsername);

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return new AccountLoginResult(AccountLoginStatus.MissingUsername);
        }

        var accounts = await _accounts.FindByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (accounts.Count == 0)
        {
            return new AccountLoginResult(AccountLoginStatus.MissingUsername);
        }

        if (accounts.Count != 1 ||
            string.IsNullOrEmpty(request.Password) ||
            !await PasswordBelongsToOwnerAsync(request.Password, ownerUsername, cancellationToken).ConfigureAwait(false))
        {
            return new AccountLoginResult(
                AccountLoginStatus.WrongPassword,
                PasswordDiagnostic: await CreatePasswordDiagnosticAsync(request, accounts, cancellationToken)
                    .ConfigureAwait(false));
        }

        var account = accounts[0];
        if (account.BannedId != AllowedBannedId)
        {
            return new AccountLoginResult(
                AccountLoginStatus.Banned,
                account,
                AccountPasswordMatchKind.LegacySavedPasswordOwnerRebind);
        }

        if (account.IsOnline)
        {
            return new AccountLoginResult(
                AccountLoginStatus.AlreadyOnline,
                account,
                AccountPasswordMatchKind.LegacySavedPasswordOwnerRebind);
        }

        return new AccountLoginResult(
            AccountLoginStatus.Success,
            account,
            AccountPasswordMatchKind.LegacySavedPasswordOwnerRebind);
    }

    private async ValueTask<AccountLoginResult> ValidateAsync(
        AccountLoginRequest request,
        AccountPasswordCompatibility compatibility,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return new AccountLoginResult(AccountLoginStatus.MissingUsername);
        }

        var accounts = await _accounts.FindByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (accounts.Count == 0)
        {
            return new AccountLoginResult(AccountLoginStatus.MissingUsername);
        }

        var matchingAccounts = accounts
            .Select(candidate => new AccountPasswordMatch(candidate, GetPasswordMatchKind(candidate, request, compatibility)))
            .Where(match => match.Kind != AccountPasswordMatchKind.None)
            .ToArray();

        if (matchingAccounts.Length == 0)
        {
            return new AccountLoginResult(
                AccountLoginStatus.WrongPassword,
                PasswordDiagnostic: await CreatePasswordDiagnosticAsync(request, accounts, cancellationToken)
                    .ConfigureAwait(false));
        }

        if (matchingAccounts.Length > 1)
        {
            return new AccountLoginResult(AccountLoginStatus.AmbiguousAccount);
        }

        var account = matchingAccounts[0].Account;

        if (account.BannedId != AllowedBannedId)
        {
            return new AccountLoginResult(AccountLoginStatus.Banned, account, matchingAccounts[0].Kind);
        }

        if (account.IsOnline)
        {
            return new AccountLoginResult(AccountLoginStatus.AlreadyOnline, account, matchingAccounts[0].Kind);
        }

        return new AccountLoginResult(AccountLoginStatus.Success, account, matchingAccounts[0].Kind);
    }

    private static AccountPasswordMatchKind GetPasswordMatchKind(
        AccountRecord candidate,
        AccountLoginRequest request,
        AccountPasswordCompatibility compatibility)
    {
        if (string.Equals(candidate.Password, request.Password, StringComparison.Ordinal))
        {
            return AccountPasswordMatchKind.Exact;
        }

        if (compatibility == AccountPasswordCompatibility.LegacyTrailingNoise &&
            HasLegacyTrailingPasswordNoise(candidate.Password, request.Password))
        {
            return AccountPasswordMatchKind.LegacyTrailingNoise;
        }

        return AccountPasswordMatchKind.None;
    }

    private static bool HasLegacyTrailingPasswordNoise(string expectedPassword, string requestPassword)
    {
        const int legacyTrailingNoiseLength = 2;

        return requestPassword.Length == expectedPassword.Length + legacyTrailingNoiseLength &&
            requestPassword.StartsWith(expectedPassword, StringComparison.Ordinal) &&
            IsAsciiLetterOrDigit(requestPassword[^2]) &&
            IsAsciiLetterOrDigit(requestPassword[^1]);
    }

    private static bool IsAsciiLetterOrDigit(char value)
    {
        return value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private async ValueTask<AccountLoginPasswordDiagnostic> CreatePasswordDiagnosticAsync(
        AccountLoginRequest request,
        IReadOnlyList<AccountRecord> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return new AccountLoginPasswordDiagnostic(
                request.Password.Length,
                CandidateCount: 0,
                BestStoredLength: 0,
                BestCommonPrefixLength: 0,
                BestLengthDelta: request.Password.Length,
                TailShape: "none");
        }

        var best = accounts
            .Select(account => new PasswordCandidateDiagnostic(
                account.Password.Length,
                CommonPrefixLength(account.Password, request.Password)))
            .OrderByDescending(candidate => candidate.CommonPrefixLength)
            .ThenBy(candidate => Math.Abs(request.Password.Length - candidate.StoredLength))
            .First();

        var exactPasswordOwners = await FindExactPasswordOwnersAsync(request.Password, cancellationToken)
            .ConfigureAwait(false);
        var firstExactPasswordOwner = exactPasswordOwners.FirstOrDefault();

        return new AccountLoginPasswordDiagnostic(
            request.Password.Length,
            accounts.Count,
            best.StoredLength,
            best.CommonPrefixLength,
            request.Password.Length - best.StoredLength,
            GetTailShape(request.Password, best.StoredLength, best.CommonPrefixLength),
            exactPasswordOwners.Count,
            firstExactPasswordOwner?.Id,
            firstExactPasswordOwner?.Username);
    }

    private async ValueTask<IReadOnlyList<AccountPasswordOwnerDiagnostic>> FindExactPasswordOwnersAsync(
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password) ||
            _accounts is not IAccountPasswordDiagnosticRepository diagnosticRepository)
        {
            return [];
        }

        return await diagnosticRepository.FindByPasswordAsync(password, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> PasswordBelongsToOwnerAsync(
        string password,
        string ownerUsername,
        CancellationToken cancellationToken)
    {
        var owners = await FindExactPasswordOwnersAsync(password, cancellationToken).ConfigureAwait(false);
        return owners.Any(owner => string.Equals(owner.Username, ownerUsername, StringComparison.OrdinalIgnoreCase));
    }

    private static int CommonPrefixLength(string expectedPassword, string requestPassword)
    {
        var count = Math.Min(expectedPassword.Length, requestPassword.Length);
        for (var index = 0; index < count; index++)
        {
            if (expectedPassword[index] != requestPassword[index])
            {
                return index;
            }
        }

        return count;
    }

    private static string GetTailShape(string requestPassword, int storedLength, int commonPrefixLength)
    {
        if (commonPrefixLength < storedLength || requestPassword.Length <= storedLength)
        {
            return "none";
        }

        return string.Concat(requestPassword[storedLength..].Select(ToCharacterClass));
    }

    private static char ToCharacterClass(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return 'D';
        }

        if (value is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
        {
            return 'L';
        }

        return char.IsWhiteSpace(value) ? 'W' : 'O';
    }

    private enum AccountPasswordCompatibility
    {
        Strict,
        LegacyTrailingNoise,
    }

    private readonly record struct AccountPasswordMatch(
        AccountRecord Account,
        AccountPasswordMatchKind Kind);

    private readonly record struct PasswordCandidateDiagnostic(
        int StoredLength,
        int CommonPrefixLength);
}
