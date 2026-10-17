namespace WolfConquer.Core.Accounts;

public interface IAccountPasswordDiagnosticRepository
{
    ValueTask<IReadOnlyList<AccountPasswordOwnerDiagnostic>> FindByPasswordAsync(
        string password,
        CancellationToken cancellationToken = default);
}
