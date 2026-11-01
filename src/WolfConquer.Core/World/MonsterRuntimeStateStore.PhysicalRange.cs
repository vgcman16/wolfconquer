using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    private WorldServerFrameHandler? physicalRangeFrame;
    private WorldDataCatalog? physicalRangeCatalog;
    internal object PhysicalAiPublicationGate => _gate;

    internal void ConfigurePhysicalRangeOwner(WorldServerFrameHandler frame, WorldDataCatalog catalog)
    {
        lock (_gate)
        {
            if (physicalRangeFrame is not null ||
                !frame.IsMonsterPhysicalRangeStore(this, catalog))
                throw new InvalidOperationException("monsterPhysicalRangeStartupOwnerInvalid");
            physicalRangeFrame = frame; physicalRangeCatalog = catalog;
        }
    }

    private MonsterPhysicalRangeState? CapturePhysicalRangeBirthNoLock(LegacyMonsterInstance instance,
        Monster? registeredFamily = null)
    {
        if (physicalRangeCatalog is not { } catalog || physicalRangeFrame is not { } frame ||
            !frame.IsMonsterPhysicalRangeStore(this, catalog)) return null;
        try
        {
            Monster? family = registeredFamily;
            if (family is null)
            {
                if (instance.DynamicId != 0 || !catalog.MonsterSpawnsByMapId.TryGetValue(instance.MapId, out var spawns)) return null;
                var matches = LegacyStaticMonsterMaterializer.Enumerate(catalog, spawns)
                    .Where(row => row.Instance.Uid == instance.Uid).ToArray();
                if (matches.Length != 1 || matches[0].Instance != instance) return null;
                family = matches[0].MonsterFamily;
            }
            var metadata = new LegacyMonsterPhysicalRangeMetadata(catalog, family);
            if (instance.IsFloor || instance.MonsterId != metadata.FamilyId ||
                instance.MaxLife != metadata.MaximumHitpoints || instance.Hitpoints != instance.MaxLife ||
                instance.Boss != (metadata.Boss != 0) || instance.Name != family.Name) return null;
            return new(instance, family, metadata);
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException)
        {
            // Existing visibility/component behavior is unchanged. An invalid
            // birth never receives physical range authority, even on a later read.
            return null;
        }
    }

    internal bool TryReadPhysicalRange(WorldServerFrameHandler frame, WorldDataCatalog catalog,
        LegacyMonsterInstance instance, Monster family, out int range)
    {
        range = 0;
        lock (_gate)
        {
            if (!ReferenceEquals(frame, physicalRangeFrame) || !ReferenceEquals(catalog, physicalRangeCatalog) ||
                !frame.IsMonsterPhysicalRangeStore(this, catalog) ||
                !_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) ||
                state.DeadAt is not null || state.Hitpoints == 0 || state.RemovedFromMapOnDeath ||
                state.PhysicalRange is not { } physical || !physical.IsCurrentPlain(catalog, instance, family)) return false;
            range = physical.Range; return true;
        }
    }

    internal LegacyPhysicalTargetPresence ReadRegisteredPhysicalTarget(uint uid, int mapId, uint dynamicId,
        out LegacyMonsterInstance current, out Monster family, out int range)
    {
        current = null!; family = null!; range = 0;
        lock (_gate)
        {
            var matches = _states.Where(row => row.Key.Uid == uid && row.Key.MapId == mapId &&
                row.Key.DynamicId == dynamicId).ToArray();
            if (matches.Length == 0) return LegacyPhysicalTargetPresence.Absent;
            if (matches.Length != 1) return LegacyPhysicalTargetPresence.Ambiguous;
            var state = matches[0].Value;
            if (state.DeadAt is not null || state.Hitpoints == 0 || state.RemovedFromMapOnDeath ||
                physicalRangeCatalog is not { } catalog || physicalRangeFrame is not { } frame ||
                !frame.IsMonsterPhysicalRangeStore(this, catalog) || state.PhysicalRange is not { } physical ||
                !physical.IsCurrentPlain(catalog, physical.Birth, physical.Family)) return LegacyPhysicalTargetPresence.Unavailable;
            current = physical.Birth with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            family = physical.Family; range = physical.Range;
            return LegacyPhysicalTargetPresence.Live;
        }
    }

    internal bool TryAdvanceOwnedPhysicalTargetLock(WorldServerFrameHandler frame,
        WorldServerFrameHandler.MonsterPhysicalAiPhase phase,
        out LegacyMonsterTargetLockResult result)
    {
        result = default;
        lock (_gate)
        {
            if (!frame.TryReadMonsterPhysicalAiPhasePlain(this, phase, out var catalog, out var inputs)) return false;
            var instance = inputs.Instance; var family = inputs.Family; var target = inputs.TargetSnapshot;
            if (!ReferenceEquals(frame, physicalRangeFrame) || !ReferenceEquals(catalog, physicalRangeCatalog) ||
                !frame.IsMonsterPhysicalRangeStore(this, catalog) ||
                !_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) ||
                state.DeadAt is not null || state.Hitpoints == 0 || state.RemovedFromMapOnDeath ||
                state.X != instance.X || state.Y != instance.Y || state.Hitpoints != instance.Hitpoints ||
                state.PhysicalRange is not { } physical || !physical.IsCurrentPlain(catalog, instance, family) ||
                !inputs.ConsumePlain())
                return false;
            // This is the real retained-target AI phase, not a range read or
            // current-HP heuristic in an attacking player's admission path.
            if (target is { } current && state.TargetLock.LastTargetUid == current.Uid &&
                physical.Metadata.Boss == 1 && state.Hitpoints >= 2_000_000)
                physical.Range = 18;
            result = LegacyMonsterTargetLock.DecideNextAction(state.TargetLock, target,
                LegacyMonsterTargetLockLifecycle.Alive, physical.Range, inputs.ScanRange,
                false, state.Hitpoints, inputs.Now, inputs.AttackInterval);
            state.TargetLock = result.State;
            state.LastAttackAt = result.State.LastAttackAt;
            state.LastAttackTargetUid = result.State.LastTargetUid;
            return true;
        }
    }

    private sealed class MonsterPhysicalRangeState(LegacyMonsterInstance birth, Monster family,
        LegacyMonsterPhysicalRangeMetadata metadata)
    {
        internal LegacyMonsterInstance Birth => birth;
        internal Monster Family => family;
        internal readonly LegacyMonsterPhysicalRangeMetadata Metadata = metadata;
        internal int Range = metadata.BirthRange;
        internal bool IsCurrentPlain(WorldDataCatalog catalog, LegacyMonsterInstance current, Monster actualFamily) =>
            ReferenceEquals(family, actualFamily) && Metadata.IsCurrentPlain(catalog, actualFamily) &&
            (current with { X = birth.X, Y = birth.Y, Hitpoints = birth.Hitpoints }) == birth;
    }
}
