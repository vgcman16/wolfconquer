namespace WolfConquer.Core.Networking;

public sealed partial class ConquerTcpServer
{
    private bool _shutdownProducersSettled, _stopDisposed;

    public async ValueTask DisposeAsync()
    {
        await _shutdownGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var failures = new List<Exception>();
            Task? acceptTask;
            CancellationTokenSource? stop;
            lock (_gate)
            {
                acceptTask = _acceptTask; stop = _stop;
                _publicStopping = true;
            }
            if (!_shutdownProducersSettled)
            {
                if (stop is not null)
                {
                    try { await stop.CancelAsync().ConfigureAwait(false); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                try { _listener.Stop(); }
                catch (Exception failure) { failures.Add(failure); }
                // Compatibility filter applies only to the accept producer,
                // never to client/EOF retirement or its retained failures.
                try { await IgnoreShutdownExceptionAsync(acceptTask).ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); }
                await SettleClientProducersAsync().ConfigureAwait(false);
                _shutdownProducersSettled = true;
                lock (_gate)
                {
                    if (!_publicStopped)
                    {
                        _publicStopped = true;
                        _publicStoppedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
            }
            if (stop is not null && !_stopDisposed)
            {
                try { stop.Dispose(); _stopDisposed = true; }
                catch (Exception failure) { failures.Add(failure); }
            }
            failures.AddRange(await RetryClientRetirementsHeldAsync().ConfigureAwait(false));
            if (failures.Count != 0) lock (_gate) _publicFaulted = true;
            ThrowRetirementFailures(failures);
        }
        finally { _shutdownGate.Release(); }
    }

    private async ValueTask SettleClientProducersAsync()
    {
        // Acceptance is already terminal: every in-flight accepted client is
        // registered before this snapshot, including a synchronous completion.
        TrackedClientTask[] clients;
        lock (_gate) clients = _clientTaskObservations.Values.ToArray();
        try { await Task.WhenAll(clients.Select(client => client.Task)).ConfigureAwait(false); }
        catch
        {
            // Every actual task, including all faults in WhenAll, is observed
            // individually below. Failures propagate through the retained owner.
        }
        foreach (var client in clients) ObserveClientTask(client);
    }
}
