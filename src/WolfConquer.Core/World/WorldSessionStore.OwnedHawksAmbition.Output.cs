using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Preallocated by the real combined writer. It observes only the exact
    // successfully committed caster nonce; it grants no new publication.
    internal sealed class OwnedHawksOutputReceipt
    {
        private readonly WorldSessionStore store;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldPacketHandlerContext source;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly object identity;
        private readonly WorldPacketHandlers.OwnedHawksAmbitionCast cast;

        internal OwnedHawksOutputReceipt(WorldSessionStore store, WorldServerFrameHandler frame,
            WorldPacketHandlerContext source, LegacyOrdinarySpellBinding binding, object identity,
            WorldPacketHandlers.OwnedHawksAmbitionCast cast)
        {
            this.store = store; this.frame = frame; this.source = source;
            this.binding = binding; this.identity = identity; this.cast = cast;
            if (source.Session is not { } before || !ReferenceEquals(store, binding.Store) ||
                !ReferenceEquals(identity, binding.NativeIdentity) || !ReferenceEquals(source.Sessions, store) ||
                !ReferenceEquals(source.Connection, binding.Connection) ||
                !ReferenceEquals(before.NativeGameClient, binding.Client) ||
                !ReferenceEquals(store.framePersistenceOwner, frame) || !cast.IsCurrentPlain(binding, before))
                throw InvalidSpellPublication();
        }

        // The constructor binds this exact private cast while it is current;
        // its historical consumed marker survives legitimate lease/EOF cleanup.
        internal bool HasCommittedOutcome => cast.WasCommitted;

        internal bool IsHistoricalOutcomeFor(WorldServerFrameHandler actualFrame, WorldSessionStore actualStore,
            ConquerTcpConnection actualConnection) => HasCommittedOutcome && ReferenceEquals(frame, actualFrame) &&
            ReferenceEquals(store, actualStore) && ReferenceEquals(source.Connection, actualConnection) &&
            ReferenceEquals(binding.Store, store) && ReferenceEquals(binding.Connection, actualConnection) &&
            ReferenceEquals(binding.NativeIdentity, identity) && ReferenceEquals(source.Session?.NativeGameClient, binding.Client);

        internal Exception OutputFailure(string reason) =>
            LegacyCommittedHawksOutputException.FromFailure(this, new InvalidOperationException(reason));

        internal void RequireCurrent(WorldPacketHandlerContext context)
        {
            if (!TryCurrent(context, out _))
                throw OutputFailure("committedHawksAmbitionOutputOwnerLost");
        }

        internal OwnedHawksOutputPhase CapturePhase(WorldPacketHandlerContext context) =>
            OwnedHawksOutputPhase.Capture(this, context);

        internal bool TryCurrent(WorldPacketHandlerContext context, out WorldSession current)
        {
            current = null!;
            if (!cast.WasCommitted || !ReferenceEquals(context.Connection, source.Connection) ||
                !ReferenceEquals(context.Sessions, store) || !ReferenceEquals(store.framePersistenceOwner, frame) ||
                !store.TryGet(source.Connection, out var observed) || observed is null ||
                !frame.TryPrepareActionContext(store, context, observed, out var actual)) return false;
            RefuseBoundSpellReentry(source.Connection);
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, observed);
                if (!store._connectionGates.TryGetValue(source.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    if (!frame.IsActionLifecycleCurrent(store, actual) ||
                        !store.IsSpellFileWriteOwnerCurrent(source.Connection, observed, identity, binding) ||
                        !OwnedHawks.TryGetValue(binding, out var slot) || slot.Lease is not { } lease ||
                        !ReferenceEquals(lease.SourceCast, cast) || !TryCaptureOwnedHawksStatus(binding, observed, out var status) ||
                        status is null || !status.IsCurrentPlain() ||
                        context.OwnedHawksPhase is { } phase && !phase.IsCurrentPlain(this, observed)) return false;
                    current = observed; return true;
                }
            }
        }
    }
}
