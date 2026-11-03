using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    // Auth repository controls only: no native owner, Store, ticket or Frame
    // is manufactured from a detached row or from a successful result object.
    private static async Task RunAuthenticationCustodyAsync(Func<AuthenticationCustodyFixture, Task> body)
    {
        var fixture = new AuthenticationCustodyFixture();
        var errors = new List<Exception>();
        try { await fixture.InitializeAsync(); await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        await fixture.CleanupAsync(errors);
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static void AssertAuthenticationRefused(SqlAccountAuthenticationAcquireResult result,
        SqlAccountAuthenticationFailure failure)
    {
        Assert.False(result.IsAcquired);
        Assert.Null(result.Custody);
        Assert.Equal(failure, result.Failure);
    }

    private sealed class AuthenticationCustodyFixture
    {
        internal const uint FirstAccount = 501, SecondAccount = 502, DuplicateAccount = 503;
        private readonly List<OwnedModernSqlAuthDatabase> databases = [];
        private readonly List<MySqlConnection> domains = [];
        private readonly List<MySqlTransaction> transactions = [];
        private readonly ConcurrentBag<SqlAccountAuthenticationCustody> owners = [];
        internal OwnedModernSqlAuthDatabase Database => databases[0];

        internal async Task InitializeAsync() { await CreateOtherDatabaseAsync(); }

        internal async Task<OwnedModernSqlAuthDatabase> CreateOtherDatabaseAsync()
        {
            var database = await OwnedModernSqlAuthDatabase.CreateAsync(FirstAccount, SecondAccount);
            databases.Add(database);
            return database;
        }

        internal async Task<MySqlConnection> OpenDomainAsync(OwnedModernSqlAuthDatabase? database = null,
            bool? ignoreCommandTransaction = null)
        {
            var connection = await (database ?? Database).OpenOwnedAuthenticationDomainAsync(ignoreCommandTransaction);
            domains.Add(connection);
            return connection;
        }

        internal async Task<MySqlTransaction> BeginDomainTransactionAsync(MySqlConnection connection)
        {
            var transaction = await connection.BeginTransactionAsync();
            transactions.Add(transaction);
            return transaction;
        }

        internal async Task<SqlAccountAuthenticationAcquireResult> AcquireAsync(
            string username = OwnedModernSqlAuthDatabase.FirstUsername,
            string password = OwnedModernSqlAuthDatabase.SyntheticPassword,
            CancellationToken token = default, OwnedModernSqlAuthDatabase? database = null)
        {
            var acquired = await (database ?? Database).CreateRepository()
                .TryAcquireAuthenticationAsync(new AccountLoginRequest(username, password), token);
            if (acquired.Custody is { } owner) owners.Add(owner);
            return acquired;
        }

        internal async Task<SqlAccountAuthenticationCustody> RequireOwnerAsync(
            string username = OwnedModernSqlAuthDatabase.FirstUsername,
            string password = OwnedModernSqlAuthDatabase.SyntheticPassword,
            OwnedModernSqlAuthDatabase? database = null)
        {
            var acquired = await AcquireAsync(username, password, database: database);
            Assert.True(acquired.IsAcquired);
            Assert.Equal(SqlAccountAuthenticationFailure.None, acquired.Failure);
            return Assert.IsType<SqlAccountAuthenticationCustody>(acquired.Custody);
        }

        // A successful acquisition owns a real SQL connection immediately. Each
        // actual result is registered before assertions, including bad results.
        internal async Task<SqlAccountAuthenticationCustody> ReacquireClosedOwnerAsync(
            string username = OwnedModernSqlAuthDatabase.FirstUsername,
            string password = OwnedModernSqlAuthDatabase.SyntheticPassword)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (true)
            {
                var acquired = await AcquireAsync(username, password, deadline.Token);
                if (acquired.IsAcquired)
                {
                    Assert.Equal(SqlAccountAuthenticationFailure.None, acquired.Failure);
                    return Assert.IsType<SqlAccountAuthenticationCustody>(acquired.Custody);
                }
                AssertAuthenticationRefused(acquired, SqlAccountAuthenticationFailure.SessionConflict);
                await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
            }
        }

        internal async Task CleanupAsync(List<Exception> errors)
        {
            foreach (var transaction in transactions)
                try { await transaction.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var owner in owners)
                try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var domain in domains)
                try { await domain.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var database in databases)
            {
                var accountIds = owners.Select(owner => owner.AccountId)
                    .Append(FirstAccount).Append(SecondAccount).Append(DuplicateAccount).Distinct().ToArray();
                try { await database.AssertAuthenticationClaimsReleasedAsync(accountIds); }
                catch (Exception error) { errors.Add(error); }
                try { await database.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            }
        }
    }
}
