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

    // Issued only at the actual private loop's selected phase, after its real
    // instance/family/current target resolution. No caller timestamp/event API.
    private MonsterPhysicalAiPhase? PrepareMonsterPhysicalAiPhase(MonsterPhysicalAiTick tick,
        LegacyMonsterInstance instance, Monster family, WorldActiveSession? target,
        LegacyMonsterTargetSnapshot? snapshot, int scanRange, TimeSpan interval)
    {
        if (_worldData is not { } catalog || !ReferenceEquals(Volatile.Read(ref currentMonsterPhysicalAiTick), tick) ||
            snapshot is { } current && (target is null || !IsMonsterPhysicalAiTargetCurrent(_monsterStates, instance, target, current))) return null;
        var clock = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        var inputs = new MonsterPhysicalAiInputs(instance, family, target, snapshot, scanRange, clock.GetUtcNow(), interval);
        var phase = new MonsterPhysicalAiPhase();
        monsterPhysicalAiPhases.Add(phase, new(tick, catalog, clock, inputs));
        return phase;
    }

    internal bool TryReadMonsterPhysicalAiPhasePlain(MonsterRuntimeStateStore store, MonsterPhysicalAiPhase phase,
        out WorldDataCatalog catalog, out MonsterPhysicalAiInputs inputs)
    {
        catalog = null!; inputs = null!;
        if (!ReferenceEquals(store, _monsterStates) || !monsterPhysicalAiPhases.TryGetValue(phase, out var actual) ||
            !ReferenceEquals(Volatile.Read(ref currentMonsterPhysicalAiTick), actual.Tick) ||
            !ReferenceEquals(_worldData, actual.Catalog) ||
            !ReferenceEquals(_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System, actual.Clock) ||
            actual.Inputs.TargetSnapshot is { } snapshot && (actual.Inputs.Target is not { } target ||
                target.Session.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !_sessions.IsMonsterPhysicalAiTargetHeld(binding, target) ||
                !IsMonsterPhysicalAiTargetCurrent(store, actual.Inputs.Instance, target, snapshot))) return false;
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
        out LegacyMonsterTargetLockResult result)
    {
        result = default;
        return TryReadMonsterPhysicalAiPhaseTarget(store, phase, out var target) && target is null &&
            _monsterStates.TryAdvanceOwnedPhysicalTargetLock(this, phase, out result);
    }

    internal bool AdvanceMonsterPhysicalAiWithTarget(WorldSessionStore store, MonsterPhysicalAiPhase phase,
        LegacyOrdinarySpellBinding binding, WorldActiveSession target, out LegacyMonsterTargetLockResult result)
    {
        result = default;
        return ReferenceEquals(store, _sessions) && store.IsMonsterPhysicalAiTargetHeld(binding, target) &&
            TryReadMonsterPhysicalAiPhaseTarget(store, phase, out var captured) && ReferenceEquals(captured, target) &&
            _monsterStates.TryAdvanceOwnedPhysicalTargetLock(this, phase, out result);
    }
}
