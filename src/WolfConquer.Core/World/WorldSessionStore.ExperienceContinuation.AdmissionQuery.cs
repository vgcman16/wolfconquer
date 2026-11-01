namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryPrepareAdmissionContinuationSuccessor(WorldPacketHandlerContext context,
        WorldPacketHandlers.CompletedAdmissionContinuationQuery previous,
        out WorldPacketHandlers.PreparedAdmissionContinuationQuery? prepared)
    {
        prepared = null;
        if (!previous.TryCurrent(out var expected) || expected.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(binding.Store, this) || !ReferenceEquals(context.Connection, binding.Connection) ||
            framePersistenceOwner is not { } frame || !frame.IsOrdinaryContinuationLoginContext(binding.Connection, context)) return false;
        RefuseBoundSpellReentry(binding.Connection);
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                if (!previous.TryCurrent(out var current) || !ReferenceEquals(current, expected) ||
                    !IsAdmissionContinuationQueryOwnerHeld(binding, expected, context) ||
                    !WorldPacketHandlers.PreparedAdmissionContinuationQuery.TryPrepareSuccessor(context, previous, out var plan) || plan is null) return false;
                string characterPath = Path.Combine(binding.UsersRoot, expected.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini");
                string[] paths = plan.LockedSpellPath is { } spellPath ? [characterPath, spellPath] : [characterPath];
                bool valid = WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    previous.TryCurrent(out var latest) && ReferenceEquals(latest, expected) &&
                    IsAdmissionContinuationQueryOwnerHeld(binding, expected, context) && plan.IsCurrentPlain());
                if (!valid) return false;
                prepared = plan; return true;
            }
        }
    }

    internal bool TryPrepareAdmissionContinuationQuery(WorldPacketHandlerContext context,
        LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin,
        out WorldPacketHandlers.PreparedAdmissionContinuationQuery? prepared)
    {
        prepared = null;
        var binding = origin.Binding; var expected = origin.Admitted;
        RefuseBoundSpellReentry(binding.Connection);
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(context.Connection, binding.Connection) ||
            framePersistenceOwner is not { } frame || !frame.IsOrdinaryContinuationLoginContext(binding.Connection, context) ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual) || !ReferenceEquals(actual, context)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                if (!origin.IsCurrentPlain() || !IsAdmissionContinuationQueryOwnerHeld(binding, expected, context) ||
                    !WorldPacketHandlers.PreparedAdmissionContinuationQuery.TryPrepare(context, origin, out var plan) || plan is null) return false;
                string characterPath = Path.Combine(binding.UsersRoot, expected.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini");
                string[] paths = plan.LockedSpellPath is { } spellPath ? [characterPath, spellPath] : [characterPath];
                bool valid = WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                    if (!origin.IsCurrentPlain() || !IsAdmissionContinuationQueryOwnerHeld(binding, expected, context) || !plan.IsCurrentPlain()) return false;
                    BeginCommittedContinuationRetirement(context, binding, expected, origin.CopyAdmittedImage());
                    return true;
                });
                if (!valid) return false;
                prepared = plan; return true;
            }
        }
    }

    // Requires the actual registered login operation, never an arbitrary
    // exclusive lease or a context copied from another admitted connection.
    internal bool IsAdmissionContinuationQueryOwnerHeld(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context) => ReferenceEquals(binding.Store, this) &&
        ReferenceEquals(context.Sessions, this) && ReferenceEquals(context.Connection, binding.Connection) &&
        framePersistenceOwner is { } frame && frame.IsOrdinaryContinuationLoginContextCurrent(context, expected) &&
        frame.IsActionLifecycleCurrent(this, context) &&
        _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && _sessions.TryGetValue(binding.Connection, out var holder) &&
        BindingIsCurrentPlain(binding, holder, expected);

    internal bool IsAdmissionContinuationQueryOwnerCurrent(LegacyOrdinarySpellBinding binding, WorldSession expected,
        WorldPacketHandlerContext context, LegacyNativeMagicQueryClient.CompletedMaximums? completion)
    {
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(context.Sessions, this) ||
            !ReferenceEquals(context.Connection, binding.Connection) || framePersistenceOwner is not { } frame ||
            !_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        if (IsCurrentThreadInsideAnyConnectionGate)
        {
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

        bool OwnerCurrent() => frame.IsOrdinaryContinuationLoginContextCurrent(context, expected) &&
            !binding.IsRetired && !binding.Recovery.IsBlocked && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
            ReferenceEquals(binding.Player.Owner, binding.Client) && ReferenceEquals(expected.NativeGameClient, binding.Client) &&
            _sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.Read(), expected) &&
            ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) &&
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
