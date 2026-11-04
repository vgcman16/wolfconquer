using System.IO.Pipelines;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Networking;

public sealed partial class ConquerTcpServer
{
    private readonly SemaphoreSlim _shutdownGate = new(1, 1);
    private readonly Dictionary<ConquerTcpClientRetirement, PendingClientRetirement> _clientRetirements = [];

    private sealed class PendingClientRetirement(ConquerTcpClientRetirement cleanup, Exception failure)
    {
        internal readonly ConquerTcpClientRetirement Cleanup = cleanup;
        internal readonly ExceptionDispatchInfo OriginalFailure = ExceptionDispatchInfo.Capture(failure);
        // Accessed only by the serialized concrete server retry/shutdown owner.
        internal bool OriginalReported;
    }

    internal (int Pending, int Producers) ReadClientRetirementCounts()
    {
        lock (_gate) return (_clientRetirements.Count, _clientTasks.Count);
    }

    private async ValueTask RetireClientAsync(ConquerTcpConnection connection, PipeReader reader,
        CancellationTokenSource stop, Action markClosed, TrackedClientTask tracked, Exception? pipelineFailure)
    {
        var retirement = new ConquerTcpClientRetirement(connection, reader);
        Exception? failure = null;
        try { markClosed(); }
        catch (Exception error) { failure = error; }
        try { await stop.CancelAsync().ConfigureAwait(false); }
        catch (Exception error) { failure = ConquerTcpClientRetirement.Combine(failure, error); }
        failure = ConquerTcpClientRetirement.Combine(failure, await retirement.TryCompleteAsync().ConfigureAwait(false));
        if (failure is null) return;
        // A caught/logged pipeline primary must not be replaced by a later
        // EOF/reader error. Pipeline-only behavior remains unchanged.
        failure = ConquerTcpClientRetirement.Combine(pipelineFailure, failure);

        // New retirement diagnostics never render exception messages, SQL values,
        // claims or resource objects. Actual errors stay privately owned below.
        try { LogClientRetirementFailure(failure); }
        catch (Exception error) { failure = ConquerTcpClientRetirement.Combine(failure, error); }
        lock (_gate)
        {
            _publicFaulted = true;
            if (retirement.HasPendingCleanup)
            {
                _clientRetirements.Add(retirement, new(retirement, failure!));
                tracked.RetirementFailure = failure;
            }
        }
        ExceptionDispatchInfo.Capture(failure!).Throw();
    }

    private void LogClientRetirementFailure(Exception failure)
    {
        string type = NormalizeLogToken(failure.GetType().Name);
        string line = "PARITY-BUG tcp retirement failure stage=transport-retirement exceptionType=" + type;
        Exception? loggingFailure = null;
        try { _log.WriteLine(line); }
        catch (Exception error) { loggingFailure = error; }
        try { _errorLog.WriteLine(line); }
        catch (Exception error) { loggingFailure = ConquerTcpClientRetirement.Combine(loggingFailure, error); }
        if (loggingFailure is not null) ExceptionDispatchInfo.Capture(loggingFailure).Throw();
    }

    internal async ValueTask RetryFailedClientRetirementsAsync()
    {
        await _shutdownGate.WaitAsync().ConfigureAwait(false);
        try { ThrowRetirementFailures(await RetryClientRetirementsHeldAsync().ConfigureAwait(false)); }
        finally { _shutdownGate.Release(); }
    }

    private async ValueTask<List<Exception>> RetryClientRetirementsHeldAsync()
    {
        var failures = DrainClientTaskFailures();
        PendingClientRetirement[] pending;
        lock (_gate) pending = _clientRetirements.Values.ToArray();
        foreach (var entry in pending)
        {
            var retry = await entry.Cleanup.TryCompleteAsync().ConfigureAwait(false);
            if (retry is not null)
            {
                failures.Add(ConquerTcpClientRetirement.Combine(entry.OriginalFailure.SourceException, retry)!);
                entry.OriginalReported = true;
            }
            else if (!entry.OriginalReported)
            {
                failures.Add(entry.OriginalFailure.SourceException);
                entry.OriginalReported = true;
            }
            if (!entry.Cleanup.HasPendingCleanup)
                lock (_gate) _clientRetirements.Remove(entry.Cleanup);
        }
        return failures;
    }

    private static void ThrowRetirementFailures(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    }
}
