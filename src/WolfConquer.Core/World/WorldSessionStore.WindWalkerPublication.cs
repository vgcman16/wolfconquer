using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class WindWalkerPublication
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly object identity;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot after;
        private readonly WorldSession proposed;
        private readonly ulong[] words;
        private readonly byte[]? frame;
        private bool committed;

        private WindWalkerPublication(WorldSessionStore store, WorldPacketHandlerContext context, object identity,
            LegacyOrdinarySpellBinding binding, LegacyNativeStatusFlagsBigVector32 vector,
            LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot after, WorldSession proposed, bool changed)
        {
            this.store = store; this.context = context; this.identity = identity; this.binding = binding;
            this.vector = vector; this.after = after; this.proposed = proposed;
            words = LegacyWindWalkerStatusProjection.Read(proposed.Character);
            if (changed) frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(checked((uint)binding.Uid), words).ToFrameBytes();
        }

        internal static WindWalkerPublication Prepare(WorldSessionStore store, WorldPacketHandlerContext context,
            object identity, LegacyOrdinarySpellBinding binding, LegacyNativeStatusFlagsBigVector32 vector,
            LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot after, WorldSession proposed, bool changed) =>
            new(store, context, identity, binding, vector, after, proposed, changed);

        internal bool MatchesCommitted(WorldSessionStore actualStore, ConquerTcpConnection connection, object actualIdentity,
            LegacyOrdinarySpellBinding actualBinding, LegacyNativeStatusFlagsBigVector32 actualVector,
            LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot actualAfter, WorldSession published) =>
            ReferenceEquals(store, actualStore) && ReferenceEquals(context.Connection, connection) && ReferenceEquals(identity, actualIdentity) &&
            ReferenceEquals(binding, actualBinding) && ReferenceEquals(vector, actualVector) && ReferenceEquals(after, actualAfter) &&
            ReferenceEquals(proposed, published) && vector.IsWindWalkerCurrentPlain(after);

        internal void ConfirmCommitted(WindWalkerPublicationProof proof)
        {
            if (!proof.IsCommittedPublication(this)) throw InvalidSpellPublication();
            committed = true;
        }

        private bool TryCurrent(out WorldSession current)
        {
            current = null!;
            if (!committed || context.Trades?.HasConnectionEnded(context.Connection) != false ||
                !LegacyNativeClientPublication.TryCurrent(store, context.Connection, binding.Uid, identity, out current) ||
                !ReferenceEquals(current.NativeGameClient, binding.Client) || binding.IsRetired || binding.Recovery.IsBlocked ||
                !ReferenceEquals(binding.Client.Player, binding.Player) || !ReferenceEquals(binding.Player.BitVector, vector) ||
                !LegacyWindWalkerStatusProjection.Read(current.Character).AsSpan().SequenceEqual(words)) return false;
            lock (vector.SyncRoot) return vector.IsWindWalkerCurrentPlain(after) &&
                ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && !binding.IsRetired;
        }

        internal async ValueTask<WorldSession?> PublishAsync(CancellationToken cancellationToken)
        {
            if (!TryCurrent(out var current)) return null;
            if (frame is null) return current;
            // Publication is already committed. Any delivery failure is a lost
            // notification, never permission to reissue/renew/undo the flag.
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!TryCurrent(out current)) return null;
            var viewers = new List<(ConquerTcpConnection Connection, long Uid, object Identity)>();
            foreach (var candidate in store.Snapshot())
            {
                long uid = candidate.Session.Character.Uid;
                if (ReferenceEquals(candidate.Connection, context.Connection) || uid == binding.Uid ||
                    !LegacyNativeClientPublication.TryCaptureIdentity(store, candidate.Connection, uid, out var peerIdentity) ||
                    !LegacyNativeClientPublication.TryCurrent(store, candidate.Connection, uid, peerIdentity, out var peer) ||
                    !WorldPacketHandlers.WindWalkerViewerVisible(current.Character, peer.Character)) continue;
                viewers.Add((candidate.Connection, uid, peerIdentity));
            }
            foreach (var viewer in viewers)
            {
                if (!TryCurrent(out current)) return null;
                if (!LegacyNativeClientPublication.TryCurrent(store, viewer.Connection, viewer.Uid, viewer.Identity, out var peer) ||
                    !WorldPacketHandlers.WindWalkerViewerVisible(current.Character, peer.Character)) continue;
                await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!TryCurrent(out current)) return null;
            }
            return current;
        }
    }
}
