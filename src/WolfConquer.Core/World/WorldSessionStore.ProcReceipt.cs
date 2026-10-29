using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ProcReceipt
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly object identity;
        private readonly LegacyOrdinarySpellBinding? binding;
        private readonly ProcParticipant? peer;
        internal WorldSession Published { get; }
        internal ProcReceipt(WorldSessionStore store, ConquerTcpConnection connection, object identity,
            LegacyOrdinarySpellBinding? binding, WorldSession published, ProcParticipant? peer)
        { this.store = store; this.connection = connection; this.identity = identity; this.binding = binding; Published = published; this.peer = peer; }

        internal bool TryCurrent(out WorldSession current) =>
            LegacyNativeClientPublication.TryCurrent(store, connection, Published.Character.Uid, identity, out current);
        internal bool RequirePreparedCurrent()
        {
            if (PreparedStateCurrent()) return true;
            FenceInterruptedPublication(); return false;
        }

        internal async ValueTask<bool> SendAsync(byte[][] frames, CancellationToken cancellationToken)
        {
            if (!PreparedStateCurrent()) { FenceInterruptedPublication(); return false; }
            foreach (var frame in frames)
            {
                if (!PreparedStateCurrent()) { FenceInterruptedPublication(); return false; }
                try { await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false); }
                catch (Exception error) { throw Failure(error); }
                if (!PreparedStateCurrent()) { FenceInterruptedPublication(); return false; }
            }
            return true;
        }
        internal bool PeerIdentityIsCurrent() => peer is not { } observed ||
            LegacyNativeClientPublication.TryCurrent(store, observed.Connection, observed.Expected.Character.Uid, observed.Identity, out _);
        internal bool TryAdvanceEffect(WorldSession expected, WorldSession proposed, out ProcReceipt? next)
        {
            next = null;
            return ReferenceEquals(expected, Published) && PreparedStateCurrent() &&
                store.TryCommitProc(new(connection, expected, identity), proposed, peer, out next);
        }
        private bool PreparedStateCurrent() => (binding is null || store.IsBoundSpellOwnerCurrent(binding)) &&
            TryCurrent(out var current) && ReferenceEquals(current, Published) &&
            (peer is not { } observed || LegacyNativeClientPublication.TryCurrent(store, observed.Connection,
                observed.Expected.Character.Uid, observed.Identity, out var currentPeer) && ReferenceEquals(currentPeer, observed.Expected) &&
                (observed.Expected.NativeGameClient?.OrdinarySpellBinding is not { } peerBinding || store.IsBoundSpellOwnerCurrent(peerBinding)));
        private void FenceInterruptedPublication()
        {
            // Component-only Stores have no saved/native issuance to quarantine.
            // A real bound issuance is fenced without touching a replacement.
            if (binding is null) return;
            var failure = Failure(new InvalidOperationException("committedProcPreparedStateChangedOrParticipantLost"));
            if (failure.FencingFailure is not null) throw failure;
        }
        internal WorldSessionPublicationException Failure(Exception cause)
        {
            var failure = new WorldSessionPublicationException(store, connection, identity, binding, Published,
                WorldSessionPublicationStage.ProcContinuation, cause);
            store.FenceObserverFailure(failure); return failure;
        }
    }
}
