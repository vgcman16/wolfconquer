using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    internal bool TryClaimFrame(WorldServerFrameHandler actualFrame, CharacterSnapshotLoader? loader,
        string? mailboxRoot, out string reason)
    {
        lock (gate)
        {
            reason = "ownedAdmissionRuntimeBindingUnavailable";
            if (closed || stopping || frame is not null || loader is null ||
                !SameRoute(configured.MailboxRoot, mailboxRoot) ||
                !Ordinary.IsCurrent(LegacyOrdinaryAdmissionRoots.FromLoader(loader)) ||
                !Ordinary.IsReconciliationComplete || !Recovery.IsReadyForOrdinaryAdmission(configured.Roots)) return false;
            frame = actualFrame; reason = string.Empty; return true;
        }
    }

    internal CancellationToken ShutdownToken => stop.Token;
    internal object LifetimeGate => gate;
    internal bool IsStopping { get { lock (gate) return stopping || closed; } }
    private static readonly AsyncLocal<LegacyOwnedAdmissionRuntime?> ExecutingWorker = new();

    internal IDisposable EnterWorker()
    {
        var previous = ExecutingWorker.Value;
        ExecutingWorker.Value = this;
        return new WorkerScope(previous);
    }

    private sealed class WorkerScope(LegacyOwnedAdmissionRuntime? previous) : IDisposable
    { public void Dispose() => ExecutingWorker.Value = previous; }
}
