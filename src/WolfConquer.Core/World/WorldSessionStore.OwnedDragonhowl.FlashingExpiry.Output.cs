using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class DragonhowlFlashingExpiryOutput
    {
        private readonly WorldSessionStore store;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly object identity;
        private readonly LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication removal;
        private readonly byte[] bytes;
        private bool committed, outputStarted;
        internal WorldSession Session { get; }

        internal DragonhowlFlashingExpiryOutput(WorldSessionStore store, WorldServerFrameHandler frame,
            WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldSession after, object identity,
            LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication removal)
        {
            this.store = store; this.frame = frame; this.context = context; this.binding = binding;
            Session = after; this.identity = identity; this.removal = removal;
            bytes = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(checked((uint)binding.Uid),
                LegacyPlayerDeathState.CreateStatusFlagVector(after.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        }

        internal void ConfirmCommitted()
        {
            if (!CurrentHeld()) throw InvalidSpellPublication();
            committed = true;
        }

        private bool CurrentHeld() => frame.IsActionLifecycleCurrent(store, context) &&
            context.TradeOperations?.OwnsActiveLease(context.FrameOperationLease) == true &&
            store._sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.Read(), Session) &&
            ReferenceEquals(holder.NativeClientIdentity, identity) && ReferenceEquals(holder.SpellBinding, binding) &&
            store.BindingIsCurrentPlain(binding, holder, Session) && removal.IsPublishedCurrentPlain();

        private bool IsCurrent()
        {
            if (!committed) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                    store.PeekMailboxSession(binding.Connection));
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var nativeGate = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    lock (binding.Player.BitVector.SyncRoot) return CurrentHeld();
                }
            }
        }

        internal async ValueTask PublishAsync(CancellationToken cancellationToken)
        {
            if (outputStarted) throw InvalidSpellPublication();
            outputStarted = true;
            try
            {
                if (!IsCurrent()) throw InvalidSpellPublication();
                await binding.Connection.SendAsync(bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                if (!IsCurrent()) throw InvalidSpellPublication();
                foreach (var candidate in store.Snapshot())
                {
                    if (ReferenceEquals(candidate.Connection, binding.Connection) || candidate.Session.Character.Uid == binding.Uid ||
                        !WorldPacketHandlers.IsOwnedDragonhowlObservedPlayer(Session, candidate.Session)) continue;
                    if (!store.TryGet(candidate.Connection, out var current) || !ReferenceEquals(current, candidate.Session)) continue;
                    await candidate.Connection.SendAsync(bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                    if (!IsCurrent()) throw InvalidSpellPublication();
                }
            }
            catch (Exception error) { Fence(error); throw; }
        }

        internal void Fence(Exception primary)
        {
            var errors = new List<Exception> { primary };
            try
            {
                lock (frame.RequireActionLifecycleGate(store))
                {
                    using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                        store.PeekMailboxSession(binding.Connection));
                    try { binding.Recovery.Block("ownedDragonhowlFlashingExpiryPublicationUnproven"); }
                    catch (Exception error) { errors.Add(error); }
                    if (store._connectionGates.TryGetValue(binding.Connection, out var gate)) lock (gate)
                    {
                        using var nativeGate = EnterNativeConnectionGate();
                        using var actors = new SpellActorScope(binding);
                        if (store._sessions.TryGetValue(binding.Connection, out var holder) &&
                            ReferenceEquals(holder.NativeClientIdentity, identity) && ReferenceEquals(holder.SpellBinding, binding) &&
                            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
                            ReferenceEquals(binding.Client.Player, binding.Player))
                            try { holder.Clear(); } catch (Exception error) { errors.Add(error); }
                    }
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
                try { binding.Recovery.Block("ownedDragonhowlFlashingExpiryCleanupFailed"); }
                catch (Exception failure) { errors.Add(failure); }
            }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(primary).Throw();
            throw new AggregateException("Committed flashing expiry and cleanup failed.", errors);
        }
    }
}
