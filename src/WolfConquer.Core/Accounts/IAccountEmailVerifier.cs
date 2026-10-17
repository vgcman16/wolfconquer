namespace WolfConquer.Core.Accounts;

public interface IAccountEmailVerifier
{
    ValueTask<bool> MatchesEmailAsync(
        long accountId,
        string email,
        CancellationToken cancellationToken = default);
}
