namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsAttributeRebuildQueryOwnerCurrent(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context)
    {
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(context.Sessions, this) ||
            !ReferenceEquals(context.Connection, binding.Connection) || framePersistenceOwner is not { } frame ||
            !_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        if (IsCurrentThreadInsideAnyConnectionGate)
        {
            // The real query mutator already owns package -> this connection.
            // Never acquire the earlier lifecycle gate from this branch.
            if (!Monitor.IsEntered(gate)) return false;
            using var actors = new SpellActorScope(binding);
            return OwnerCurrent();
        }
        if (Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot))
            return false; // Do not reverse actor -> lifecycle/connection order.
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                return frame.IsActionLifecycleCurrent(this, context) && OwnerCurrent();
            }
        }

        bool OwnerCurrent() => !binding.IsRetired && !binding.Recovery.IsBlocked &&
            ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
            ReferenceEquals(binding.Player.Owner, binding.Client) && ReferenceEquals(expected.NativeGameClient, binding.Client) &&
            _sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.Read(), expected) &&
            ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) &&
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
            frame.IsOwnedPhysicalMetadataCatalogCurrent(this, binding, context.WorldData) && IsBoundSpellOwnerCurrent(binding);
    }

    internal bool IsAttributeRebuildOwnerHeld(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context) =>
        ReferenceEquals(binding.Store, this) && framePersistenceOwner is { } frame &&
        frame.IsActionLifecycleCurrent(this, context) && context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
        _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && _sessions.TryGetValue(binding.Connection, out var holder) &&
        BindingIsCurrentPlain(binding, holder, expected);
}
