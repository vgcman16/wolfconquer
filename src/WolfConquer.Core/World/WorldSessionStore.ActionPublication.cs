namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Prepared by the actual stance Store path before its joint native/holder
    // commit. It is a notification receipt, never permission to repeat a write.
    internal sealed class ActionPublication
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly WorldServerFrameHandler frame;
        private readonly object identity;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyActionState.NativeValueProof after;
        private readonly uint action;
        private readonly LegacyNativeActorScalarState.NativeValueProof scalars;
        private readonly WorldPacketHandlers.OwnedMovementProjection? movement;
        private bool committed;
        internal WorldSession Published { get; }

        internal ActionPublication(WorldSessionStore store, WorldPacketHandlerContext context, WorldServerFrameHandler frame,
            object identity, LegacyOrdinarySpellBinding binding, WorldSession published, uint action,
            WorldPacketHandlers.OwnedMovementProjection? movement = null)
        {
            this.store = store; this.context = context; this.frame = frame; this.identity = identity;
            this.binding = binding; Published = published; this.action = action;
            this.movement = movement; scalars = new(published.Character);
            after = new(published.Character);
            if (after.Value != action || !after.IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal void ConfirmCommitted()
        {
            if (!frame.IsActionLifecycleCurrent(store, context) || !store.TryGet(context.Connection, out var current) ||
                !ReferenceEquals(current, Published) || !store.IsBoundSpellOwnerCurrent(binding) ||
                !NativeMatches() || !after.IsCurrentPlain()) throw InvalidSpellPublication();
            committed = true;
        }

        internal bool TryCurrent(out WorldSession current)
        {
            current = null!;
            if (!committed) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                    store.PeekMailboxSession(context.Connection));
                if (!store._connectionGates.TryGetValue(context.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var nativeGate = EnterNativeConnectionGate();
                    if (!store._sessions.TryGetValue(context.Connection, out var holder) ||
                        !ReferenceEquals(holder.Read(), Published) || !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                        !ReferenceEquals(holder.SpellBinding, binding) || !frame.IsActionLifecycleCurrent(store, context)) return false;
                    using var actors = new SpellActorScope(binding);
                    if (!store.BindingIsCurrentPlain(binding, holder, Published) || !NativeMatches() ||
                        !after.IsCurrentPlain()) return false;
                    current = Published;
                    return true;
                }
            }
        }

        // The 2203 Character image is already durable when this is called.
        // Fence the shared recovery owner and clear only this exact binding;
        // a later replacement holder is never cleared on the old receipt.
        internal void FenceCommittedAttributeFailure(Exception primary) => FenceCommittedQueryFailure(primary,
            "ownedAttributeSetRecoveryRequired:queryPublicationUnproven", "Committed attribute allocation and recovery fencing failed.");

        internal void FenceCommittedLevelUpFailure(Exception primary) => FenceCommittedQueryFailure(primary,
            "ownedLevelUpRecoveryRequired:queryPublicationUnproven", "Committed level-up and recovery fencing failed.");

        private void FenceCommittedQueryFailure(Exception primary, string reason, string aggregateMessage)
        {
            var errors = new List<Exception> { primary };
            try
            {
                lock (frame.RequireActionLifecycleGate(store))
                {
                    using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                        store.PeekMailboxSession(context.Connection));
                    if (store._connectionGates.TryGetValue(context.Connection, out var gate))
                    {
                        lock (gate)
                        {
                            using var nativeGate = EnterNativeConnectionGate();
                            try { binding.Recovery.Block(reason); }
                            catch (Exception error) { errors.Add(error); }
                            if (store._sessions.TryGetValue(context.Connection, out var holder) &&
                                ReferenceEquals(holder.NativeClientIdentity, identity) && ReferenceEquals(holder.SpellBinding, binding))
                                try
                                {
                                    using var actors = new SpellActorScope(binding);
                                    // Revalidate after actor acquisition; a stale receipt cannot clear a replacement.
                                    if (store._sessions.TryGetValue(context.Connection, out var current) && ReferenceEquals(current, holder) &&
                                        ReferenceEquals(holder.NativeClientIdentity, identity) && ReferenceEquals(holder.SpellBinding, binding))
                                        holder.Clear();
                                }
                                catch (Exception error) { errors.Add(error); }
                        }
                    }
                    else
                        try { binding.Recovery.Block(reason); }
                        catch (Exception error) { errors.Add(error); }
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
                try { binding.Recovery.Block(reason); }
                catch (Exception fence) { errors.Add(fence); }
            }
            if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
            throw new AggregateException(aggregateMessage, errors);
        }

        private bool NativeMatches() => scalars.IsCurrentPlain() && (uint)binding.Player.Action == action &&
            binding.Player.Reborn == scalars.Value.Reborn && binding.Player.Agility == scalars.Value.Agility &&
            binding.Player.X == scalars.Value.X && binding.Player.Y == scalars.Value.Y &&
            binding.Player.Map == scalars.Value.Map && binding.Player.DynamicID == scalars.Value.DynamicID &&
            (movement is null || binding.Player.Px == movement.Expected.Character.X && binding.Player.Py == movement.Expected.Character.Y);
    }
}
