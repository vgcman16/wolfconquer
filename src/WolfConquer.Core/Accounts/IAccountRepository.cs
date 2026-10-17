namespace WolfConquer.Core.Accounts;

public interface IAccountRepository
{
    ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
