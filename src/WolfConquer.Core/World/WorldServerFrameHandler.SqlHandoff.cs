using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Explicit internal admission seam only: no opcode/configuration binding,
    // file fallback, native/Store publication or SQL Online mutation is added.
    internal async ValueTask<SqlConnectionHandoffResult> RetainSqlWorldHandoffAsync(
        ConquerTcpConnection connection, SqlWorldStateRepository repository,
        string token, ConquerEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(token);
        _ = SqlAccountWorldHandoffEndpoint.Validate(endpoint);
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = await _worldOperations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false);
        var refusal = SqlConnectionHandoffEligibility(connection);
        if (refusal != SqlAccountWorldHandoffFailure.None) return new(refusal);

        // The actual Frame pipeline must already own this transport lifetime.
        // This explicit SQL seam cannot register or replace a producer callback.
        if (!_creationLifetimes.TryGetValue(connection, out var lifetime) || !lifetime.IsCurrent(connection))
            return new(SqlAccountWorldHandoffFailure.SessionConflict);
        if (!_tradeRecovery.TryEnsureInitialized(out _) || _tradeRecovery.IsBlocked ||
            !_tradeRecovery.TryRequireLiveJournalClear(out _))
            return new(SqlAccountWorldHandoffFailure.WorldUnavailable);
        cancellationToken.ThrowIfCancellationRequested();
        if (connection.HasObservedTransportClose) return new(SqlAccountWorldHandoffFailure.SessionRetired);

        var binding = new SqlConnectionHandoffBinding(this, connection, lifetime);
        // This reservation is denial-only until concrete successful redemption.
        // Any indeterminate outcome leaves it fenced; it can never grant gameplay.
        _sqlConnectionHandoffs.Add(connection, binding);
        try
        {
            var redeemed = await repository.RedeemAccountHandoffAsync(token, endpoint, cancellationToken).ConfigureAwait(false);
            if (!redeemed.IsRedeemed)
            {
                _sqlConnectionHandoffs.Remove(connection);
                return new(redeemed.Failure);
            }
            // Retain the returned owner before any further check/allocation/await.
            binding.Custody = redeemed.Custody!;
            binding.WasRedeemed = true;
            binding.AccountId = redeemed.AccountId;
            binding.CharacterUid = redeemed.CharacterUid;
            binding.Username = redeemed.Username!;
            cancellationToken.ThrowIfCancellationRequested();
            RequireSqlConnectionHandoffCurrent(binding);
            var read = await binding.Custody.ReadBundleAsync(cancellationToken).ConfigureAwait(false);
            RequireSqlConnectionHandoffRead(binding, read);
            cancellationToken.ThrowIfCancellationRequested();
            RequireSqlConnectionHandoffCurrent(binding);
            var result = new SqlConnectionHandoffResult(SqlAccountWorldHandoffFailure.None,
                binding.AccountId, binding.CharacterUid, binding.Username);
            binding.IsRetained = true;
            return result;
        }
        catch (Exception primary)
        {
            // The repository already attempted this cleanup. Keep its exact
            // public commit classification and retry its actual retained owner
            // on EOF/release, without inventing a world owner that never returned.
            if (primary is SqlAccountWorldHandoffException { HasPendingCleanup: true } pending)
                binding.PendingCleanup = pending;
            var failure = await RetireSqlConnectionHandoffHeldAsync(binding, primary,
                retryPendingCleanup: false).ConfigureAwait(false);
            failure = binding.WasRedeemed ? SqlConnectionCommittedFailure(failure!) : failure;
            binding.Failure = ExceptionDispatchInfo.Capture(failure!);
            binding.Failure.Throw();
            throw;
        }
    }

    private SqlAccountWorldHandoffFailure SqlConnectionHandoffEligibility(ConquerTcpConnection connection)
    {
        if (connection.HasObservedTransportClose) return SqlAccountWorldHandoffFailure.SessionRetired;
        if (HasSqlConnectionHandoffFence(connection) || _successfulInternalHandoffs.TryGetValue(connection, out _))
            return SqlAccountWorldHandoffFailure.SessionConflict;
        if (_protocolProfile != ConquerProtocolProfile.VisibleDx8D13 || !connection.HasRegisteredRelicWire ||
            !IsPreCharacterConnection(connection) || connection.NativeGameClient is not { FullLoading: false, Fake: false })
            return SqlAccountWorldHandoffFailure.WorldUnavailable;
        return SqlAccountWorldHandoffFailure.None;
    }
}
