using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    public async ValueTask<SqlAccountWorldHandoffRedeemResult> RedeemAccountHandoffAsync(
        string token, ConquerEndpoint expectedEndpoint, CancellationToken cancellationToken = default)
    {
        if (!SqlAccountWorldHandoffToken.IsCanonical(token)) return RefusedHandoff(SqlAccountWorldHandoffFailure.InvalidToken);
        _ = SqlAccountWorldHandoffEndpoint.Validate(expectedEndpoint);
        byte[] hash = SqlAccountWorldHandoffToken.Hash(token);
        MySqlConnection? connection = null;
        MySqlTransaction? transaction = null;
        SqlWorldBundleCustody? owner = null;
        SqlAccountWorldHandoffRow? row = null;
        SqlAccountWorldHandoffRedeemResult? prepared = null;
        SqlAccountWorldHandoffFailure refusal = SqlAccountWorldHandoffFailure.None;
        Exception? failure = null;
        List<IAsyncDisposable>? pendingCleanup = null;
        bool commitAttempted = false, committed = false;
        try
        {
            connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await VerifyAccountHandoffSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            row = await SqlAccountWorldHandoffRow.ReadForUpdateAsync(connection, transaction, hash, cancellationToken).ConfigureAwait(false);
            if (row is null) refusal = SqlAccountWorldHandoffFailure.UnknownTicket;
            else if (row.Redeemed) refusal = SqlAccountWorldHandoffFailure.AlreadyRedeemed;
            else if (!string.Equals(row.Host, expectedEndpoint.Address, StringComparison.Ordinal) || row.Port != expectedEndpoint.Port)
                refusal = SqlAccountWorldHandoffFailure.EndpointMismatch;
            else if ((refusal = await HandoffTimeStatusAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false)) != SqlAccountWorldHandoffFailure.None) { /* Expired before redemption. */ }
            else if (!await HandoffIssuerClaimsAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false))
                refusal = SqlAccountWorldHandoffFailure.SessionRetired;
            else
            {
                refusal = await SqlAccountWorldHandoffCredentials.VerifyAsync(connection, transaction, row.AccountId,
                    row.CharacterUid, row.Username, row.Salt, row.Fingerprint, cancellationToken).ConfigureAwait(false);
                if (refusal == SqlAccountWorldHandoffFailure.None)
                {
                    var acquired = await TryAcquireBundleCustodyAsync(row.AccountId, cancellationToken).ConfigureAwait(false);
                    if (!acquired.IsAcquired)
                    {
                        refusal = acquired.Failure switch
                        {
                            SqlWorldStateFailure.MissingState => SqlAccountWorldHandoffFailure.WorldUnavailable,
                            SqlWorldStateFailure.SessionCustodyConflict => SqlAccountWorldHandoffFailure.SessionConflict,
                            SqlWorldStateFailure.CharacterIdentityConflict => SqlAccountWorldHandoffFailure.IdentityConflict,
                            _ => throw new InvalidDataException("sqlHandoffWorldStateInvalid"),
                        };
                    }
                    else
                    {
                        owner = acquired.Custody!;
                        if (owner.CharacterUid != row.CharacterUid) refusal = SqlAccountWorldHandoffFailure.IdentityConflict;
                        else if ((refusal = await owner.VerifyHandoffWorldClaimsAsync(connection, transaction, cancellationToken).ConfigureAwait(false)) != SqlAccountWorldHandoffFailure.None) { /* Preserve the actual ownership refusal. */ }
                        else if ((refusal = await HandoffTimeStatusAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false)) != SqlAccountWorldHandoffFailure.None) { /* Expired before redemption. */ }
                        else if (!await HandoffIssuerClaimsAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false))
                            refusal = SqlAccountWorldHandoffFailure.SessionRetired;
                        else
                        {
                            long redeemConnection = await HandoffConnectionIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                            int updated = await UseResourceAsync(connection.CreateCommand(), async command =>
                            {
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE wolf_sql_auth_handoff SET redeemed_utc=UTC_TIMESTAMP(6),redeemed_connection_id=@connection WHERE token_hash=@hash AND redeemed_utc IS NULL AND redeemed_connection_id IS NULL AND issued_utc <= UTC_TIMESTAMP(6) AND UTC_TIMESTAMP(6) < expires_utc";
                                command.Parameters.AddWithValue("@hash", hash);
                                command.Parameters.AddWithValue("@connection", redeemConnection);
                                int count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                                return count;
                            }).ConfigureAwait(false);
                            if (updated == 0)
                            {
                                refusal = await HandoffTimeStatusAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false);
                                if (refusal == SqlAccountWorldHandoffFailure.None)
                                    throw new InvalidDataException("sqlHandoffUpdateCountInvalid");
                            }
                            else if (updated != 1) throw new InvalidDataException("sqlHandoffUpdateCountInvalid");
                            else if ((refusal = await HandoffTimeStatusAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false)) != SqlAccountWorldHandoffFailure.None) { /* Expired after update; rollback. */ }
                            else if (!await HandoffIssuerClaimsAsync(connection, transaction, row, cancellationToken).ConfigureAwait(false))
                                refusal = SqlAccountWorldHandoffFailure.SessionRetired;
                            else if ((refusal = await owner.VerifyHandoffWorldClaimsAsync(connection, transaction, cancellationToken).ConfigureAwait(false)) != SqlAccountWorldHandoffFailure.None) { /* Preserve the actual ownership refusal. */ }
                            else
                            {
                                prepared = new SqlAccountWorldHandoffRedeemResult(owner, row.AccountId, row.CharacterUid, row.Username,
                                    SqlAccountWorldHandoffFailure.None);
                                commitAttempted = true;
                                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                                committed = true;
                                if (await owner.VerifyHandoffWorldClaimsAsync(connection, null, cancellationToken).ConfigureAwait(false) != SqlAccountWorldHandoffFailure.None)
                                    throw new SqlAccountWorldHandoffCommittedCleanupException(new InvalidOperationException("sqlHandoffWorldOwnerLost"));
                            }
                        }
                    }
                }
            }
            if (!commitAttempted) await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = commitAttempted && !committed ? new SqlAccountWorldHandoffCommitOutcomeUnknownException(error) :
                committed && error is not SqlAccountWorldHandoffCommittedCleanupException ? new SqlAccountWorldHandoffCommittedCleanupException(error) : error;
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
                failure = CombineHandoffCleanup(failure, cleanup, committed);
            }
        }
        if (connection is not null)
        {
            try { await connection.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            {
                (pendingCleanup ??= []).Add(connection);
                failure = CombineHandoffCleanup(failure, cleanup, committed);
            }
        }
        CryptographicOperations.ZeroMemory(hash);
        if (failure is not null || !committed)
        {
            if (owner is not null)
            {
                try { await owner.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup)
                {
                    (pendingCleanup ??= []).Add(owner);
                    failure = CombineHandoffCleanup(failure, cleanup, committed);
                }
            }
            if (failure is not null)
            {
                // A raw acquisition can be the only pending resource even
                // when every resource local to redemption closed successfully.
                failure = SqlAccountWorldHandoffCleanupTransfer.RetainCleanup(failure,
                    pendingCleanup, commitAttempted, committed);
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            return RefusedHandoff(refusal);
        }
        return prepared!;
    }

    private static Exception CombineHandoffCleanup(Exception? primary, Exception cleanup, bool committed)
    {
        Exception next = committed ? new SqlAccountWorldHandoffCommittedCleanupException(cleanup) : cleanup;
        return primary is null ? next : new AggregateException(primary, next);
    }

    private static SqlAccountWorldHandoffRedeemResult RefusedHandoff(SqlAccountWorldHandoffFailure failure) =>
        new(null, 0, 0, null, failure);

    private static async ValueTask<SqlAccountWorldHandoffFailure> HandoffTimeStatusAsync(MySqlConnection connection,
        MySqlTransaction transaction, SqlAccountWorldHandoffRow row, CancellationToken token)
    {
        DateTime now = await HandoffServerUtcAsync(connection, transaction, token).ConfigureAwait(false);
        if (now < row.IssuedUtc) throw new InvalidDataException("sqlHandoffServerTimeBeforeIssue");
        return now >= row.ExpiresUtc ? SqlAccountWorldHandoffFailure.Expired : SqlAccountWorldHandoffFailure.None;
    }
    private static ValueTask<DateTime> HandoffServerUtcAsync(MySqlConnection connection, MySqlTransaction transaction, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT UTC_TIMESTAMP(6)";
            object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value is not DateTime date) throw new InvalidDataException("sqlHandoffServerTimeInvalid");
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        });

    private static ValueTask<long> HandoffConnectionIdAsync(MySqlConnection connection, MySqlTransaction transaction, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT CAST(CONNECTION_ID() AS UNSIGNED)";
            object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value is null or DBNull) throw new InvalidDataException("sqlHandoffConnectionIdInvalid");
            ulong id;
            try { id = Convert.ToUInt64(value, CultureInfo.InvariantCulture); }
            catch (Exception error) when (error is FormatException or OverflowException or InvalidCastException)
            { throw new InvalidDataException("sqlHandoffConnectionIdInvalid", error); }
            if (id == 0 || id > long.MaxValue) throw new InvalidDataException("sqlHandoffConnectionIdInvalid");
            return (long)id;
        });
    private static async ValueTask<bool> HandoffIssuerClaimsAsync(MySqlConnection connection, MySqlTransaction transaction,
        SqlAccountWorldHandoffRow row, CancellationToken token)
    {
        string database = await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT DATABASE()";
            object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value is not string name || name.Length == 0) throw new InvalidDataException("sqlHandoffDatabaseInvalid");
            return name;
        }).ConfigureAwait(false);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..24];
        string account = "wolf:auth:" + digest + ":a:" + row.AccountId.ToString(CultureInfo.InvariantCulture);
        return await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT CAST(IS_USED_LOCK(@account) AS SIGNED),CAST(IS_USED_LOCK(@child) AS SIGNED)";
            command.Parameters.AddWithValue("@account", account);
            command.Parameters.AddWithValue("@child", row.Claim);
            return await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) || reader.IsDBNull(1)) return false;
                bool same = reader.GetInt64(0) == row.IssuerConnectionId && reader.GetInt64(1) == row.IssuerConnectionId;
                if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("sqlHandoffClaimResultInvalid");
                return same;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}