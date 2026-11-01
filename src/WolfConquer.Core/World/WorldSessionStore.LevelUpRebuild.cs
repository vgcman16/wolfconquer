namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsLevelUpRebuildOwnerHeld(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context) => ReferenceEquals(binding.Store, this) &&
        ReferenceEquals(context.Sessions, this) && ReferenceEquals(context.Connection, binding.Connection) &&
        framePersistenceOwner is { } frame && frame.IsActionLifecycleCurrent(this, context) &&
        context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
        _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && _sessions.TryGetValue(binding.Connection, out var holder) &&
        BindingIsCurrentPlain(binding, holder, expected);

    internal bool IsLevelUpRebuildQueryOwnerCurrent(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context, LegacyNativeMagicQueryClient.CompletedMaximums? completion)
    {
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(context.Sessions, this) ||
            !ReferenceEquals(context.Connection, binding.Connection) || framePersistenceOwner is not { } frame ||
            !_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        if (IsCurrentThreadInsideAnyConnectionGate)
        {
            // Only the exact recipient's query mutator may take this branch.
            // Never acquire an earlier lifecycle gate while holding any peer gate.
            if (!Monitor.IsEntered(gate)) return false;
            using var actors = new SpellActorScope(binding);
            return OwnerCurrent();
        }
        if (Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot)) return false;
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
            ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) && ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
            ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
            ReferenceEquals(expected.NativeGameClient, binding.Client) &&
            _sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.Read(), expected) &&
            ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) &&
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
            frame.IsOwnedPhysicalMetadataCatalogCurrent(this, binding, context.WorldData) && IsBoundSpellOwnerCurrent(binding) &&
            binding.Player.Level == expected.Character.Level && expected.Character.Experience >= 0 &&
            binding.Player.Experience == (ulong)expected.Character.Experience &&
            binding.Player.Strength == expected.Character.Strength && binding.Player.Agility == expected.Character.Agility &&
            binding.Player.Vitality == expected.Character.Vitality && binding.Player.Spirit == expected.Character.Spirit &&
            binding.Player.Atributes == expected.Character.AttributePoints &&
            binding.Player.HitPoints == expected.Character.MinHitPoints && binding.Player.Mana == expected.Character.MinMana &&
            (completion is null || completion.IsCurrentPlain(binding, expected));
    }
}
