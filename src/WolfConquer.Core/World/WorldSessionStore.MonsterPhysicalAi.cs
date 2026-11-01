namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryAdvanceMonsterPhysicalAi(WorldServerFrameHandler frame,
        WorldServerFrameHandler.MonsterPhysicalAiPhase phase, out LegacyMonsterTargetLockResult result)
    {
        result = default;
        if (!ReferenceEquals(framePersistenceOwner, frame) ||
            !frame.TryReadMonsterPhysicalAiPhaseTarget(this, phase, out var target)) return false;
        if (target is null) return frame.AdvanceMonsterPhysicalAiWithoutTarget(this, phase, out result);
        var context = frame.CreateMonsterRuntimeContext(target);
        if (!frame.TryPrepareActionContext(this, context, target.Session, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, target.Session);
            if (!_connectionGates.TryGetValue(target.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(target.Connection, out var holder) || !ReferenceEquals(holder.Read(), target.Session) ||
                    holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, target.Session) ||
                    !frame.IsActionLifecycleCurrent(this, actual)) return false;
                using var actors = new SpellActorScope(binding);
                lock (binding.Player.BitVector.SyncRoot)
                    return frame.AdvanceMonsterPhysicalAiWithTarget(this, phase, binding, target, out result);
            }
        }
    }

    internal bool IsMonsterPhysicalAiTargetHeld(LegacyOrdinarySpellBinding binding, WorldActiveSession target) =>
        ReferenceEquals(binding.Store, this) && ReferenceEquals(binding.Connection, target.Connection) &&
        _connectionGates.TryGetValue(target.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.BitVector.SyncRoot) &&
        _sessions.TryGetValue(target.Connection, out var holder) && ReferenceEquals(holder.Read(), target.Session) &&
        ReferenceEquals(holder.SpellBinding, binding) && BindingIsCurrentPlain(binding, holder, target.Session);
}
