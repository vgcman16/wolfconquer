using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed partial class SqlAccountAuthenticationCustody
{
    private bool handoffReserved;

    // The private gate owns reservation, credential row locks, claim proof and COMMIT.
    internal async ValueTask<SqlAccountWorldHandoffIssued>
        IssueHandoffAsync(ConquerEndpoint endpoint, TimeSpan lifetime, CancellationToken token)
    {
        byte[] host = SqlAccountWorldHandoffEndpoint.Validate(endpoint);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (connection is null) return new(SqlAccountWorldHandoffFailure.SessionRetired, null, default, default);
            if (handoffReserved) return new(SqlAccountWorldHandoffFailure.SessionConflict, null, default, default);
            handoffReserved = true;
            var verified = await VerifyAndRetireHeldAsync(token).ConfigureAwait(false);
            if (verified != SqlAccountAuthenticationFailure.None)
                return new(MapHandoffFailure(verified), null, default, default);
            byte[] usernameBytes;
            try { usernameBytes = new System.Text.UTF8Encoding(false, true).GetBytes(Username); }
            catch (System.Text.EncoderFallbackException) { return new(SqlAccountWorldHandoffFailure.InvalidAccountState, null, default, default); }
            if (usernameBytes.Length is 0 or > 1024)
                return new(SqlAccountWorldHandoffFailure.InvalidAccountState, null, default, default);
            var actual = connection!;
            await SqlWorldStateRepository.VerifyAccountHandoffSchemaAsync(actual, token).ConfigureAwait(false);
            byte[] random = RandomNumberGenerator.GetBytes(24);
            string child;
            try { child = "wolf:auth:h:" + Convert.ToHexString(random); }
            finally { CryptographicOperations.ZeroMemory(random); }
            if (!await AcquireClaimAsync(actual, child, token).ConfigureAwait(false))
                return new(SqlAccountWorldHandoffFailure.SessionConflict, null, default, default);

            MySqlTransaction? transaction = null;
            Exception? failure = null;
            List<IAsyncDisposable>? pendingCleanup = null;
            bool commitAttempted = false, committed = false;
            string? bearer = null;
            DateTime issued = default, expires = default;
            SqlAccountWorldHandoffFailure refusal = SqlAccountWorldHandoffFailure.None;
            try
            {
                transaction = await actual.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token).ConfigureAwait(false);
                refusal = await SqlAccountWorldHandoffCredentials.VerifyAsync(actual, transaction, AccountId,
                    CharacterUid, Username, salt, passwordFingerprint, token).ConfigureAwait(false);
                if (refusal == SqlAccountWorldHandoffFailure.None &&
                    !await ClaimsAreCurrentAsync(actual, transaction, identity, true, token).ConfigureAwait(false))
                    refusal = SqlAccountWorldHandoffFailure.SessionRetired;
                if (refusal == SqlAccountWorldHandoffFailure.None)
                {
                    var rows = await MySqlAccountRepository.ReadHandoffAuthenticationRowsForUpdateAsync(actual, transaction, Username, token).ConfigureAwait(false);
                    if (rows.Count != 1) throw new InvalidDataException("sqlHandoffAccountChangedWhileLocked");
                    byte[] ticketSalt = RandomNumberGenerator.GetBytes(32);
                    byte[]? ticketFingerprint = null;
                    byte[]? hash = null;
                    try
                    {
                        ticketFingerprint = SqlAccountWorldHandoffCredentials.Fingerprint(ticketSalt, rows[0].Password);
                        bearer = SqlAccountWorldHandoffToken.Create();
                        hash = SqlAccountWorldHandoffToken.Hash(bearer);
                        issued = await UseSqlAsync<MySqlCommand, DateTime>(actual.CreateCommand(), async cmd =>
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "SELECT UTC_TIMESTAMP(6)";
                            object? value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                            if (value is not DateTime date) throw new InvalidDataException("sqlHandoffServerTimeInvalid");
                            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
                        }).ConfigureAwait(false);
                        expires = issued.Add(lifetime);
                        await UseSqlAsync<MySqlCommand, int>(actual.CreateCommand(), async cmd =>
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "INSERT INTO wolf_sql_auth_handoff (token_hash,account_id,character_uid,username,endpoint_host,endpoint_port,issuer_connection_id,handoff_claim,credential_salt,credential_fingerprint,issued_utc,expires_utc,redeemed_utc,redeemed_connection_id) VALUES (@hash,@account,@uid,@username,@host,@port,@issuer,@claim,@salt,@fingerprint,@issued,@expires,NULL,NULL)";
                            cmd.Parameters.AddWithValue("@hash", hash); cmd.Parameters.AddWithValue("@account", AccountId);
                            cmd.Parameters.AddWithValue("@uid", CharacterUid); cmd.Parameters.AddWithValue("@username", usernameBytes);
                            cmd.Parameters.AddWithValue("@host", host); cmd.Parameters.AddWithValue("@port", endpoint.Port);
                            cmd.Parameters.AddWithValue("@issuer", identity.ConnectionId); cmd.Parameters.AddWithValue("@claim", System.Text.Encoding.ASCII.GetBytes(child));
                            cmd.Parameters.AddWithValue("@salt", ticketSalt); cmd.Parameters.AddWithValue("@fingerprint", ticketFingerprint);
                            cmd.Parameters.AddWithValue("@issued", issued); cmd.Parameters.AddWithValue("@expires", expires);
                            int count = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            if (count != 1) throw new InvalidDataException("sqlHandoffInsertCountInvalid");
                            return count;
                        }).ConfigureAwait(false);
                        bool claimsCurrent = await ClaimsAreCurrentAsync(actual, transaction, identity, true, token).ConfigureAwait(false) &&
                            await ChildClaimIsCurrentAsync(actual, transaction, child, identity.ConnectionId, token).ConfigureAwait(false);
                        DateTime now = await UseSqlAsync<MySqlCommand, DateTime>(actual.CreateCommand(), async cmd =>
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "SELECT UTC_TIMESTAMP(6)";
                            object? value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                            if (value is not DateTime date) throw new InvalidDataException("sqlHandoffServerTimeInvalid");
                            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
                        }).ConfigureAwait(false);
                        if (!claimsCurrent || now >= expires)
                        {
                            refusal = !claimsCurrent ? SqlAccountWorldHandoffFailure.SessionRetired : SqlAccountWorldHandoffFailure.Expired;
                            bearer = null;
                            await transaction.RollbackAsync(token).ConfigureAwait(false);
                        }
                        else
                        {
                            commitAttempted = true;
                            await transaction.CommitAsync(token).ConfigureAwait(false);
                            committed = true;
                            if (!await ClaimsAreCurrentAsync(actual, null, identity, true, token).ConfigureAwait(false) ||
                                !await ChildClaimIsCurrentAsync(actual, null, child, identity.ConnectionId, token).ConfigureAwait(false))
                                throw new SqlAccountWorldHandoffCommittedCleanupException(new InvalidOperationException("sqlHandoffIssuerClaimLost"));
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(ticketSalt);
                        if (ticketFingerprint is not null) CryptographicOperations.ZeroMemory(ticketFingerprint);
                        if (hash is not null) CryptographicOperations.ZeroMemory(hash);
                    }
                }
                else await transaction.RollbackAsync(token).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                failure = commitAttempted && !committed ? new SqlAccountWorldHandoffCommitOutcomeUnknownException(error) : committed && error is not SqlAccountWorldHandoffCommittedCleanupException ? new SqlAccountWorldHandoffCommittedCleanupException(error) : error;
                if (!commitAttempted && transaction is not null)
                {
                    try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception cleanup) { failure = new AggregateException(failure, cleanup); }
                }
            }
            if (transaction is not null)
            {
                try { await transaction.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup)
                {
                    (pendingCleanup ??= []).Add(transaction);
                    if (committed) cleanup = new SqlAccountWorldHandoffCommittedCleanupException(cleanup);
                    failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
                }
            }
            if (failure is not null)
            {
                failure = await RetireHeldAsync(failure).ConfigureAwait(false);
                if (pendingCleanup is not null)
                    failure = SqlAccountWorldHandoffException.WithPendingCleanup(failure!,
                        pendingCleanup.ToArray(), commitAttempted, committed);
                ExceptionDispatchInfo.Capture(failure!).Throw();
            }
            return new(refusal, bearer, issued, expires);
        }
        finally { gate.Release(); }
    }

    private static ValueTask<bool> ChildClaimIsCurrentAsync(MySqlConnection connection, MySqlTransaction? transaction,
        string child, long issuer, CancellationToken token) => UseSqlAsync<MySqlCommand, bool>(connection.CreateCommand(), async command =>
    {
        command.Transaction = transaction;
        command.CommandText = "SELECT CAST(IS_USED_LOCK(@claim) AS SIGNED)";
        command.Parameters.AddWithValue("@claim", child);
        object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return value is not null and not DBNull && Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == issuer;
    });

    private static SqlAccountWorldHandoffFailure MapHandoffFailure(SqlAccountAuthenticationFailure failure) => failure switch
    {
        SqlAccountAuthenticationFailure.None => SqlAccountWorldHandoffFailure.None,
        SqlAccountAuthenticationFailure.MissingUsername => SqlAccountWorldHandoffFailure.MissingUsername,
        SqlAccountAuthenticationFailure.WrongPassword => SqlAccountWorldHandoffFailure.WrongPassword,
        SqlAccountAuthenticationFailure.AmbiguousAccount => SqlAccountWorldHandoffFailure.AmbiguousAccount,
        SqlAccountAuthenticationFailure.Banned => SqlAccountWorldHandoffFailure.Banned,
        SqlAccountAuthenticationFailure.AlreadyOnline => SqlAccountWorldHandoffFailure.AlreadyOnline,
        SqlAccountAuthenticationFailure.InvalidAccountState => SqlAccountWorldHandoffFailure.InvalidAccountState,
        SqlAccountAuthenticationFailure.IdentityConflict => SqlAccountWorldHandoffFailure.IdentityConflict,
        SqlAccountAuthenticationFailure.SessionConflict => SqlAccountWorldHandoffFailure.SessionConflict,
        SqlAccountAuthenticationFailure.SessionRetired => SqlAccountWorldHandoffFailure.SessionRetired,
        SqlAccountAuthenticationFailure.DomainConflict => SqlAccountWorldHandoffFailure.DomainConflict,
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}