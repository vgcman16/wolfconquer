using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The real initial holder gate owns preflight, journal commitment and first
    // publication. There is no caller-provided persistence/acceptance callback.
    internal bool TryCommitOrdinaryAdmission(ConquerTcpConnection connection, LegacyOrdinaryAdmissionPlan plan,
        out WorldSession? admitted, out string reason,
        LegacyOrdinaryAdmissionInterruption interruption = LegacyOrdinaryAdmissionInterruption.None)
    {
        admitted = null; reason = "ordinaryAdmissionStoreUnavailable";
        if (!plan.MatchesConnection(connection) || IsCurrentThreadInsideAnyConnectionGate) return false;
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, plan.Baseline);
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            var holder = _sessions.GetValue(connection, static _ => new SessionHolder());
            if (holder.Read() is not null || Snapshot().Any(row => row.Session.AccountId == plan.Baseline.AccountId) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, null, plan.Baseline, plan) || plan.Recovery.IsBlocked) return false;
            // Unbound managed callers have no fabricated native lock/owner.
            if (plan.NativeGate is { } nativeGate)
            {
                lock (nativeGate)
                lock (plan.PlayerGate!)
                    return CommitOrdinaryAdmissionLocked(connection, plan, holder, interruption, out admitted, out reason);
            }
            return CommitOrdinaryAdmissionLocked(connection, plan, holder, interruption, out admitted, out reason);
        }
    }

    private bool CommitOrdinaryAdmissionLocked(ConquerTcpConnection connection, LegacyOrdinaryAdmissionPlan plan,
        SessionHolder holder, LegacyOrdinaryAdmissionInterruption interruption, out WorldSession? admitted, out string reason)
    {
        admitted = null;
        lock (LegacySecondaryPasswordRuntime.SyncRoot)
        {
            using var lease = OrdinaryAdmissionLease.Capture(this, connection, plan.Baseline.AccountId);
            if (lease is null) { reason = "ordinaryAdmissionStoreLeaseUnavailable"; return false; }
            if (!plan.TryCommitDurable(lease, out reason, interruption) || plan.Candidate is not { } final) return false;
            plan.PublishCommittedNative();
            holder.Publish(final);
            if (!plan.InitializeCommittedPin())
            {
                // This is a COMMITTED publication failure, not a refund/rejection
                // of the already saved normalization. Retire and fence recovery.
                holder.Clear(); _sessions.Remove(connection);
                reason = "ordinaryAdmissionRecoveryRequired:committedPinPublicationUnproven";
                plan.Recovery.Block(reason); return false;
            }
            admitted = final; reason = string.Empty;
            // Initial Set's real Frame callback persists only deltas from a
            // previous owner. No previous owner exists, and all admission images
            // are already committed by the real joint writer.
            return true;
        }
    }

    internal sealed class OrdinaryAdmissionLease : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly object gate;
        private readonly long account;
        private bool active = true;
        private OrdinaryAdmissionLease(WorldSessionStore store, ConquerTcpConnection connection, SessionHolder holder, object gate, long account)
        { this.store = store; this.connection = connection; this.holder = holder; this.gate = gate; this.account = account; }
        internal static OrdinaryAdmissionLease? Capture(WorldSessionStore store, ConquerTcpConnection connection, long account) =>
            store._connectionGates.TryGetValue(connection, out var gate) && Monitor.IsEntered(gate) &&
            store._sessions.TryGetValue(connection, out var holder) && holder.Read() is null
                ? new(store, connection, holder, gate, account) : null;
        internal bool IsCurrentEmpty(ConquerTcpConnection expected) => active && Monitor.IsEntered(gate) &&
            ReferenceEquals(expected, connection) && store._sessions.TryGetValue(connection, out var actual) &&
            ReferenceEquals(actual, holder) && holder.Read() is null &&
            store.Snapshot().All(row => row.Session.AccountId != account);
        public void Dispose() => active = false;
    }
}
