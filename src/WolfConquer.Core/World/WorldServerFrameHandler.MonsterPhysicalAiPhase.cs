using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed class MonsterPhysicalAiTick(WorldServerFrameHandler frame) : IDisposable
    {
        public void Dispose()
        {
            lock (frame._monsterStates.PhysicalAiPublicationGate)
                Interlocked.CompareExchange(ref frame.currentMonsterPhysicalAiTick, null, this);
        }
    }
    internal sealed class MonsterPhysicalAiPhase { }
    internal sealed record MonsterPhysicalAiInputs(
        LegacyMonsterInstance Instance, Monster Family, WorldActiveSession? Target,
        LegacyMonsterTargetSnapshot? TargetSnapshot, int ScanRange, DateTimeOffset Now, TimeSpan AttackInterval)
    {
        private bool consumed;
        internal bool ConsumePlain()
        {
            if (consumed) return false;
            consumed = true; return true;
        }
    }
    private sealed record MonsterPhysicalAiPhaseState(MonsterPhysicalAiTick Tick, WorldDataCatalog Catalog,
        TimeProvider Clock, MonsterPhysicalAiInputs Inputs);
    private MonsterPhysicalAiTick? currentMonsterPhysicalAiTick;
    private readonly ConditionalWeakTable<MonsterPhysicalAiPhase, MonsterPhysicalAiPhaseState> monsterPhysicalAiPhases = new();

    private MonsterPhysicalAiTick BeginMonsterPhysicalAiTick()
    {
        var tick = new MonsterPhysicalAiTick(this);
        lock (_monsterStates.PhysicalAiPublicationGate)
            Interlocked.Exchange(ref currentMonsterPhysicalAiTick, tick);
        return tick;
    }

    private bool TryAdvanceSelectedMonsterPhysicalAi(MonsterPhysicalAiTick tick,
        LegacyMonsterInstance instance, Monster family,
        int scanRange, TimeSpan interval, DateTimeOffset now, out bool hasTarget, out WorldActiveSession target,
        out LegacyMonsterTargetLockResult result, out MonsterPhysicalAiRefusal reason)
    {
        // The owned stamina worker publishes a new holder under this exact
        // lifecycle gate. Select and issue inside it, rather than carrying an
        // obsolete target record into the Store after waiting for that worker.
        // Store still proves the exact holder and enters package/connection/
        // native actor/monster gates in its established order. Nothing is
        // refreshed after issuance, including a replaced binding or actor.
        lock (RequireActionLifecycleGate(_sessions))
        {
            var candidates = _sessions.Snapshot();
            _monsterStates.TryGetTargetLockState(instance, now, out var targetLock);
            hasTarget = TryFindMonsterTargetLockCandidate(candidates, instance, scanRange,
                targetLock.LastTargetUid, out target, out var snapshot);
            var phase = PrepareMonsterPhysicalAiPhase(tick, instance, family,
                hasTarget ? target : null, hasTarget ? snapshot : null, scanRange, interval, out reason);
            result = default;
            return phase is not null && _sessions.TryAdvanceMonsterPhysicalAi(this, phase, out result, out reason);
        }
    }

    // Issued only at the actual private loop's selected phase, after its real
    // instance/family/current target resolution. No caller timestamp/event API.
    private MonsterPhysicalAiPhase? PrepareMonsterPhysicalAiPhase(MonsterPhysicalAiTick tick,
        LegacyMonsterInstance instance, Monster family, WorldActiveSession? target,
        LegacyMonsterTargetSnapshot? snapshot, int scanRange, TimeSpan interval,
        out MonsterPhysicalAiRefusal reason)
    {
        reason = MonsterPhysicalAiRefusal.PhaseOwner;
        if (_worldData is not { } catalog || !ReferenceEquals(Volatile.Read(ref currentMonsterPhysicalAiTick), tick)) return null;
        if (snapshot is { } current)
        {
            reason = MonsterPhysicalAiRefusal.PhaseTarget;
            if (target is null || !IsMonsterPhysicalAiTargetCurrent(_monsterStates, instance, target, current, out reason)) return null;
        }
        var clock = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        var inputs = new MonsterPhysicalAiInputs(instance, family, target, snapshot, scanRange, clock.GetUtcNow(), interval);
        var phase = new MonsterPhysicalAiPhase();
        monsterPhysicalAiPhases.Add(phase, new(tick, catalog, clock, inputs));
        reason = MonsterPhysicalAiRefusal.None;
        return phase;
    }

    internal bool TryReadMonsterPhysicalAiPhasePlain(MonsterRuntimeStateStore store, MonsterPhysicalAiPhase phase,
        out WorldDataCatalog catalog, out MonsterPhysicalAiInputs inputs, out MonsterPhysicalAiRefusal reason)
    {
        catalog = null!; inputs = null!;
        if (!ReferenceEquals(store, _monsterStates) || !monsterPhysicalAiPhases.TryGetValue(phase, out var actual) ||
            !ReferenceEquals(Volatile.Read(ref currentMonsterPhysicalAiTick), actual.Tick) ||
            !ReferenceEquals(_worldData, actual.Catalog) ||
            !ReferenceEquals(_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System, actual.Clock))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.PhaseOwner, out reason);
        if (actual.Inputs.TargetSnapshot is { } snapshot)
        {
            if (actual.Inputs.Target is not { } target ||
                target.Session.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !_sessions.IsMonsterPhysicalAiTargetHeld(binding, target))
                return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetCustody, out reason);
            if (!IsMonsterPhysicalAiTargetCurrent(store, actual.Inputs.Instance, target, snapshot, out reason)) return false;
        }
        reason = MonsterPhysicalAiRefusal.None;
        catalog = actual.Catalog; inputs = actual.Inputs; return true;
    }

    internal bool TryReadMonsterPhysicalAiPhaseTarget(WorldSessionStore store, MonsterPhysicalAiPhase phase,
        out WorldActiveSession? target)
    {
        target = null;
        if (!ReferenceEquals(store, _sessions) || !monsterPhysicalAiPhases.TryGetValue(phase, out var actual) ||
            !ReferenceEquals(Volatile.Read(ref currentMonsterPhysicalAiTick), actual.Tick)) return false;
        target = actual.Inputs.Target; return true;
    }

    internal bool AdvanceMonsterPhysicalAiWithoutTarget(WorldSessionStore store, MonsterPhysicalAiPhase phase,
        out LegacyMonsterTargetLockResult result, out MonsterPhysicalAiRefusal reason)
    {
        result = default;
        if (!TryReadMonsterPhysicalAiPhaseTarget(store, phase, out var target) || target is not null)
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.PhaseTarget, out reason);
        return _monsterStates.TryAdvanceOwnedPhysicalTargetLock(this, phase, out result, out reason);
    }

    internal bool AdvanceMonsterPhysicalAiWithTarget(WorldSessionStore store, MonsterPhysicalAiPhase phase,
        LegacyOrdinarySpellBinding binding, WorldActiveSession target, out LegacyMonsterTargetLockResult result,
        out MonsterPhysicalAiRefusal reason)
    {
        result = default;
        if (!ReferenceEquals(store, _sessions) || !store.IsMonsterPhysicalAiTargetHeld(binding, target))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetCustody, out reason);
        if (!TryReadMonsterPhysicalAiPhaseTarget(store, phase, out var captured) || !ReferenceEquals(captured, target))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.PhaseTarget, out reason);
        return _monsterStates.TryAdvanceOwnedPhysicalTargetLock(this, phase, out result, out reason);
    }
}
