namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    public void Dispose()
    {
        lock (gate)
        {
            if (closed) return;
            if (ReferenceEquals(ExecutingWorker.Value, this) || disposal is not null)
                throw new InvalidOperationException("Owned admission requires external asynchronous shutdown.");
            BeginStop();
            // Never synchronously wait for a real actor or the caller's lease.
            // Refusal retains the registry; callers may await DisposeAsync once
            // their operation has unwound.
            IDisposable? exclusive = null;
            if (frame is not null && !frame.TryEnterOwnedAdmissionShutdown(this, out exclusive))
                throw new InvalidOperationException("Owned admission has active operations; await asynchronous shutdown.");
            using (exclusive) CloseRegistry();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (closed) return ValueTask.CompletedTask;
            if (ReferenceEquals(ExecutingWorker.Value, this))
                return ValueTask.FromException(new InvalidOperationException("An actual runtime worker cannot drain itself."));
            return new ValueTask(disposal ??= DisposeCoreAsync());
        }
    }

    private void BeginStop()
    {
        stopping = true;
        Recovery.Block("ownedAdmissionRuntimeStopping");
        stop.Cancel();
    }

    private async Task DisposeCoreAsync()
    {
        BeginStop();
        using var exclusive = frame is null ? null : await frame.EnterOwnedAdmissionShutdownAsync(this).ConfigureAwait(false);
        lock (gate) CloseRegistry();
    }

    private void CloseRegistry()
    {
        registry.Dispose();
        stop.Dispose();
        closed = true;
    }
}
