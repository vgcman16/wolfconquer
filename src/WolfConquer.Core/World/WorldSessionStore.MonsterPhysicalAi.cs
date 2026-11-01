using AiRefusal = WolfConquer.Core.World.WorldServerFrameHandler.MonsterPhysicalAiRefusal;
using static WolfConquer.Core.World.WorldServerFrameHandler;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryAdvanceMonsterPhysicalAi(WorldServerFrameHandler frame,
        WorldServerFrameHandler.MonsterPhysicalAiPhase phase, out LegacyMonsterTargetLockResult result,
        out AiRefusal reason)
    {
        result = default;
        if (!ReferenceEquals(framePersistenceOwner, frame)) return RefuseMonsterPhysicalAi(AiRefusal.StoreOwner, out reason);
        if (!frame.TryReadMonsterPhysicalAiPhaseTarget(this, phase, out var target))
            return RefuseMonsterPhysicalAi(AiRefusal.PhaseTarget, out reason);
        if (target is null) return frame.AdvanceMonsterPhysicalAiWithoutTarget(this, phase, out result, out reason);
        var context = frame.CreateMonsterRuntimeContext(target);
        if (!frame.TryPrepareActionContext(this, context, target.Session, out var actual))
            return RefuseMonsterPhysicalAi(AiRefusal.ActionContext, out reason);
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, target.Session);
            if (!_connectionGates.TryGetValue(target.Connection, out var gate))
                return RefuseMonsterPhysicalAi(AiRefusal.ConnectionGate, out reason);
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(target.Connection, out var holder) || !ReferenceEquals(holder.Read(), target.Session))
                    return RefuseMonsterPhysicalAi(AiRefusal.StoreHolder, out reason);
                if (holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, target.Session))
                    return RefuseMonsterPhysicalAi(AiRefusal.StoreBinding, out reason);
                if (!frame.IsActionLifecycleCurrent(this, actual))
                    return RefuseMonsterPhysicalAi(AiRefusal.Lifecycle, out reason);
                using var actors = new SpellActorScope(binding);
                lock (binding.Player.BitVector.SyncRoot)
                    return frame.AdvanceMonsterPhysicalAiWithTarget(this, phase, binding, target, out result, out reason);
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
