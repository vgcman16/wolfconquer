using System.Runtime.ExceptionServices;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class MySqlAccountRepository
{
    public ValueTask<SqlAccountAuthenticationAcquireResult> TryAcquireAuthenticationAsync(
        AccountLoginRequest request, CancellationToken token = default) =>
        SqlAccountAuthenticationCustody.AcquireAsync(this, request, token);

    // This factory creates provider resources, not authentication authority.
    // Only the concrete owner's successful acquisition may issue that authority.
    internal MySqlConnection CreateAuthenticationCustodyConnection()
    {
        var settings = new MySqlConnectionStringBuilder(_connectionString) { Pooling = false, TreatTinyAsBoolean = false };
        if (settings.Server.Contains(','))
            throw new InvalidOperationException("sqlAccountAuthenticationMultipleServerDomainsUnsupported");
        return new(settings.ConnectionString);
    }

    private async ValueTask<IReadOnlyList<AccountRecord>> FindAuthenticationRowsAsync(string username, CancellationToken token)
    {
        var connection = CreateUsernameReadConnection();
        IReadOnlyList<AccountRecord>? accounts = null;
        Exception? failure = null;
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            accounts = await ReadAuthenticationRowsAsync(connection, username, token).ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        failure = await DisposeAuthenticationResourceAsync(connection, failure).ConfigureAwait(false);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return accounts ?? throw new InvalidOperationException("sqlAccountAuthenticationReadMissing");
    }

    // Every resource here is an actual command/reader/connection created by the
    // concrete repository. Cleanup cannot replace the original provider/row fault.
    private static async ValueTask<Exception?> DisposeAuthenticationResourceAsync(IAsyncDisposable? resource, Exception? primary)
    {
        if (resource is null) return primary;
        try { await resource.DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { return primary is null ? cleanup : new AggregateException(primary, cleanup); }
        return primary;
    }
}
