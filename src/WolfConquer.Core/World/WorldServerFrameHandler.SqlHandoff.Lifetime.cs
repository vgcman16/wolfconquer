using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal async ValueTask<SqlWorldStateBundleReadResult?> ReadRetainedSqlWorldBundleAsync(
        ConquerTcpConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var operation = await _worldOperations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false);
        if (!_sqlConnectionHandoffs.TryGetValue(connection, out var binding)) return null;
        binding.Failure?.Throw();
        if (binding.Custody is null) return null;
        try
        {
            if (!binding.IsRetained) throw new InvalidOperationException("sqlConnectionHandoffNotRetained");
            RequireSqlConnectionHandoffCurrent(binding);
            var read = await binding.Custody.ReadBundleAsync(cancellationToken).ConfigureAwait(false);
            RequireSqlConnectionHandoffRead(binding, read);
            cancellationToken.ThrowIfCancellationRequested();
            RequireSqlConnectionHandoffCurrent(binding);
            return read;
        }
        catch (Exception primary)
        {
            var failure = await RetireSqlConnectionHandoffHeldAsync(binding, primary).ConfigureAwait(false);
            failure = SqlConnectionCommittedFailure(failure!);
            binding.Failure = ExceptionDispatchInfo.Capture(failure);
            binding.Failure.Throw();
            throw;
        }
    }

    internal async ValueTask ReleaseRetainedSqlWorldHandoffAsync(ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        // Required cleanup cannot be canceled by the closing/rejected frame.
        using var operation = await _worldOperations.EnterExclusiveAsync().ConfigureAwait(false);
        if (!_sqlConnectionHandoffs.TryGetValue(connection, out var binding)) return;
        var failure = await RetireSqlConnectionHandoffHeldAsync(binding, null).ConfigureAwait(false);
        if (failure is null) return;
        if (binding.Failure is { } prior) failure = new AggregateException(prior.SourceException, failure);
        if (binding.WasRedeemed) failure = SqlConnectionCommittedFailure(failure);
        binding.Failure = ExceptionDispatchInfo.Capture(failure);
        binding.Failure.Throw();
    }

    // The actual world-exclusive operation is held. Keep the permanent denial
    // fence and retain cleanup-only custody until actual disposal succeeds.
    private static async ValueTask<Exception?> RetireSqlConnectionHandoffHeldAsync(
        SqlConnectionHandoffBinding binding, Exception? primary, bool retryPendingCleanup = true)
    {
        binding.IsRetained = false;
        if (binding.Custody is { } owned)
        {
            try
            {
                await owned.DisposeAsync().ConfigureAwait(false);
                binding.Custody = null;
            }
            catch (Exception cleanup)
            { primary = primary is null ? cleanup : new AggregateException(primary, cleanup); }
        }
        if (retryPendingCleanup && binding.PendingCleanup is { } pending)
        {
            try { await pending.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            { primary = primary is null ? cleanup : new AggregateException(primary, cleanup); }
            finally
            {
                if (!pending.HasPendingCleanup) binding.PendingCleanup = null;
            }
        }
        return primary;
    }

    // Invoked only by the exact installed producer EOF callback. Attempt all
    // existing and SQL retirements even if an earlier one fails; never route a
    // SQL disposal failure through the old trade cleanup logger's swallow path.
    private async Task CompleteSqlAndLegacyTransportCloseAsync(ConquerTcpConnection connection)
    {
        Exception? failure = null;
        try { RetireEndedAuthenticatedCreation(connection); }
        catch (Exception error) { failure = error; }
        // Publish the existing deny-only EOF marker before waiting for any SQL
        // world lease. Queued GM/trade actions must not use the still-published
        // native/Store actor while actual owner cleanup waits for that lease.
        try
        {
            lock (_tradeStates.SyncRoot) _tradeStates.MarkConnectionEnded(connection);
        }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        try { await ReleaseRetainedSqlWorldHandoffAsync(connection).ConfigureAwait(false); }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        try { await CancelTradeAfterDisconnectAsync(CreateTradeDisconnectContext(connection), transportClosed: true).ConfigureAwait(false); }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
