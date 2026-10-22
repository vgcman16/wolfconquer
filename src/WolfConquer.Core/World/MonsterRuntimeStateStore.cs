using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    private const uint LegacyFirstDynamicMonsterUid = 900_000;
    private const uint LegacyLastDynamicMonsterUid = 999_999;
    public const int LegacyPoisonedStatusFlag = 1;

    private readonly object _gate = new();
    private readonly Random _legacyMonsterRespawnRandom = new();
    private readonly Random _legacyMonsterChaseFallbackRandom = new();
    private readonly Dictionary<MonsterRuntimeKey, MonsterRuntimeState> _states = new();
    private readonly Dictionary<uint, LegacyDynamicMonsterRuntimeEntry> _dynamicMonstersByUid = new();
    private readonly Dictionary<int, LegacyMonsterItemGeneratorState> _itemGeneratorsByMonsterId = new();
    private uint _nextDynamicMonsterUid = LegacyFirstDynamicMonsterUid;

    public bool TryGetVisibleInstance(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out LegacyMonsterInstance visible)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                (state.NativeDeathRespawnReadyAt is not null && state.Hitpoints == 0))
            {
                visible = default!;
                return false;
            }

            visible = instance with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            return true;
        }
    }

    public bool TryConsumeRespawnedInstance(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out LegacyMonsterInstance respawned)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                respawned = default!;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now, consumeRespawnTransition: true);
            if (!state.PendingRespawnBroadcast)
            {
                respawned = default!;
                return false;
            }

            state.PendingRespawnBroadcast = false;
            respawned = instance with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            return true;
        }
    }

    public bool TryConsumeDeathRemovalInstance(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out LegacyMonsterInstance removed)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) ||
                state.DeadAt is not { } deadAt ||
                state.DeathRemovalBroadcastSent ||
                now < LegacyMonsterLifecycleSchedule.FromDeath(deadAt, state.RespawnTimeSeconds).RemoveFromViewDueAt)
            {
                removed = default!;
                return false;
            }

            state.DeathRemovalBroadcastSent = true;
            removed = instance with { X = state.X, Y = state.Y, Hitpoints = 0 };
            return true;
        }
    }

    public bool TryMarkAttackDue(
        LegacyMonsterInstance instance,
        uint targetUid,
        DateTimeOffset now,
        TimeSpan attackInterval)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return false;
            }

            if (state.LastAttackAt is { } lastAttackAt &&
                now <= lastAttackAt + attackInterval)
            {
                return false;
            }

            state.LastAttackAt = now;
            state.LastAttackTargetUid = targetUid;
            return true;
        }
    }

    public LegacyMonsterTargetLockResult AdvanceTargetLock(
        LegacyMonsterInstance instance,
        LegacyMonsterTargetSnapshot? target,
        LegacyMonsterTargetLockLifecycle lifecycle,
        int attackRange,
        int scanRange,
        DateTimeOffset now,
        TimeSpan attackInterval)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);

            var effectiveLifecycle = state.DeadAt is null
                ? lifecycle
                : LegacyMonsterTargetLockLifecycle.Dead;
            var result = LegacyMonsterTargetLock.DecideNextAction(
                state.TargetLock,
                target,
                effectiveLifecycle,
                attackRange,
                scanRange,
                instance.Boss,
                state.Hitpoints,
                now,
                attackInterval);

            state.TargetLock = result.State;
            state.LastAttackAt = result.State.LastAttackAt;
            state.LastAttackTargetUid = result.State.LastTargetUid;
            return result;
        }
    }

    public bool TryGetTargetLockState(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out LegacyMonsterTargetLockState targetLock)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                targetLock = LegacyMonsterTargetLockState.Empty;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                targetLock = LegacyMonsterTargetLockState.Empty;
                return false;
            }

            targetLock = state.TargetLock;
            return true;
        }
    }

    public bool TryGetLastAttackTargetUid(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out uint targetUid)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                targetUid = 0;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                state.LastAttackTargetUid is not { } lastAttackTargetUid)
            {
                targetUid = 0;
                return false;
            }

            targetUid = lastAttackTargetUid;
            return true;
        }
    }

    public bool TryMarkBlackSpotUntil(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        DateTimeOffset blackSpotExpiresAt)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return false;
            }

            state.BlackSpotExpiresAt = blackSpotExpiresAt;
            state.PendingBlackSpotInactiveBroadcast = false;
            return true;
        }
    }

    public bool TryConsumeBlackSpotInactiveInstance(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out LegacyMonsterInstance inactive)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                inactive = default!;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            QueueExpiredBlackSpotInactiveNoLock(state, now);
            if (!state.PendingBlackSpotInactiveBroadcast)
            {
                inactive = default!;
                return false;
            }

            state.PendingBlackSpotInactiveBroadcast = false;
            inactive = instance with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            return true;
        }
    }

    public bool IsBlackSpotted(
        LegacyMonsterInstance instance,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                state.BlackSpotExpiresAt is not { } blackSpotExpiresAt)
            {
                return false;
            }

            if (now > blackSpotExpiresAt)
            {
                QueueExpiredBlackSpotInactiveNoLock(state, now);
                return false;
            }

            return true;
        }
    }

    public MonsterPoisonResult ApplyPoison(
        LegacyMonsterInstance instance,
        byte poisonLevel,
        TimeSpan duration,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Poison duration cannot be negative.");
        }

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return new MonsterPoisonResult(
                    Applied: false,
                    AlreadyDead: true,
                    BossImmune: false,
                    Poison: null);
            }

            if (instance.Boss)
            {
                return new MonsterPoisonResult(
                    Applied: false,
                    AlreadyDead: false,
                    BossImmune: true,
                    Poison: null);
            }

            var poison = new MonsterPoisonState(
                poisonLevel,
                now + duration,
                LegacyPoisonedStatusFlag);
            state.Poison = poison;

            return new MonsterPoisonResult(
                Applied: true,
                AlreadyDead: false,
                BossImmune: false,
                poison);
        }
    }

    public bool TryGetPoisonState(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        out MonsterPoisonState poison)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                poison = default;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                state.Poison is not { } activePoison)
            {
                poison = default;
                return false;
            }

            poison = activePoison;
            return true;
        }
    }

    public bool TryMoveTowardTarget(
        LegacyMonsterInstance instance,
        ushort targetX,
        ushort targetY,
        DateTimeOffset now,
        TimeSpan moveInterval,
        out LegacyMonsterInstance moved,
        out uint direction,
        Func<ushort, ushort, bool>? isCandidateTileValid = null,
        Func<uint>? nextFallbackDirection = null,
        bool allowFallback = true)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                state.X == targetX && state.Y == targetY ||
                state.LastMovedAt is { } lastMovedAt && now - lastMovedAt < moveInterval)
            {
                moved = default!;
                direction = 0;
                return false;
            }

            direction = GetLegacyDirectionToward(state.X, state.Y, targetX, targetY);
            if (!TryGetLegacyWalkCandidate(
                    state.X,
                    state.Y,
                    direction,
                    isCandidateTileValid,
                    out var candidateX,
                    out var candidateY))
            {
                if (!allowFallback)
                {
                    moved = default!;
                    direction = 0;
                    return false;
                }

                direction = GetLegacyFallbackDirection(nextFallbackDirection);
                if (!TryGetLegacyWalkCandidate(
                        state.X,
                        state.Y,
                        direction,
                        isCandidateTileValid,
                        out candidateX,
                        out candidateY))
                {
                    moved = default!;
                    direction = 0;
                    return false;
                }
            }

            state.X = candidateX;
            state.Y = candidateY;
            state.LastMovedAt = now;
            moved = instance with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            return true;
        }
    }

    public bool TryMoveTo(
        LegacyMonsterInstance instance,
        ushort targetX,
        ushort targetY,
        DateTimeOffset now,
        out LegacyMonsterInstance moved,
        Func<ushort, ushort, bool>? isCandidateTileValid = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (isCandidateTileValid is not null && !isCandidateTileValid(targetX, targetY))
        {
            moved = default!;
            return false;
        }

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                moved = default!;
                return false;
            }

            state.X = targetX;
            state.Y = targetY;
            state.LastMovedAt = now;
            moved = instance with { X = state.X, Y = state.Y, Hitpoints = state.Hitpoints };
            return true;
        }
    }

    public MonsterDamageResult ApplyDamage(
        LegacyMonsterInstance instance,
        uint requestedDamage,
        DateTimeOffset now)
    {
        return ApplyDamage(
            instance,
            requestedDamage,
            now,
            attackerUid: null,
            attackerName: null);
    }

    public MonsterDamageResult ApplyDamage(
        LegacyMonsterInstance instance,
        uint requestedDamage,
        DateTimeOffset now,
        uint attackerUid,
        string attackerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attackerName);

        return ApplyDamage(
            instance,
            requestedDamage,
            now,
            (uint?)attackerUid,
            attackerName);
    }

    private MonsterDamageResult ApplyDamage(
        LegacyMonsterInstance instance,
        uint requestedDamage,
        DateTimeOffset now,
        uint? attackerUid,
        string? attackerName,
        bool deferNativeDeathPrefix = false)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                (state.NativeDeathRespawnReadyAt is not null && state.Hitpoints == 0))
            {
                return new MonsterDamageResult(
                    DamageApplied: 0,
                    RemainingHitpoints: 0,
                    Killed: false,
                    AlreadyDead: true,
                    RequestedDamage: requestedDamage);
            }

            if (requestedDamage == 0)
            {
                return new MonsterDamageResult(
                    DamageApplied: 0,
                    RemainingHitpoints: state.Hitpoints,
                    Killed: false,
                    AlreadyDead: false,
                    RequestedDamage: requestedDamage);
            }

            var damage = Math.Min(requestedDamage, state.Hitpoints);
            if (deferNativeDeathPrefix && damage == state.Hitpoints)
            {
                // Native lethal ReceiveAttack calls Dead BEFORE subtracting HP. The real owner
                // is committed by the shared prefix after its preceding BlackSpot publication.
                return new MonsterDamageResult(damage, 0, Killed: true,
                    AlreadyDead: false, RequestedDamage: requestedDamage);
            }

            state.Hitpoints -= damage;
            var killed = state.Hitpoints == 0;
            if (killed)
            {
                if (state.BlackSpotExpiresAt is not null)
                {
                    state.PendingBlackSpotInactiveBroadcast = true;
                }

                state.NativeDeathRespawnReadyAt = null;
                state.DeadAt = now;
                state.PendingRespawnBroadcast = false;
                state.DeathRemovalBroadcastSent = false;
                state.BlackSpotExpiresAt = null;
                state.Poison = null;
                state.TimedStatusFlags.Clear();
                state.ScarOfEarth = null;
                state.TargetLock = LegacyMonsterTargetLockState.Empty;
                state.LastAttackAt = null;
                state.LastAttackTargetUid = null;
                if (instance.RemoveOnDead)
                {
                    state.RemovedFromMapOnDeath = true;
                    state.RespawnSuppressed = true;
                }
            }
            else if (instance.Boss &&
                damage != 0 &&
                attackerUid is { } uid &&
                uid != 0 &&
                !string.IsNullOrWhiteSpace(attackerName))
            {
                state.RecordBossHarm(uid, attackerName, damage);
            }

            return new MonsterDamageResult(
                damage,
                state.Hitpoints,
                killed,
                AlreadyDead: false,
                RequestedDamage: requestedDamage);
        }
    }

    public IReadOnlyList<LegacyBossHarmRankingEntry> GetBossHarmRanking(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        int maxEntries = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                return Array.Empty<LegacyBossHarmRankingEntry>();
            }

            RefreshRespawnNoLock(instance, state, now);
            return state.GetBossHarmRanking(maxEntries);
        }
    }

    public IReadOnlyList<LegacyBossHarmRankingEntry> ConsumeBossHarmRanking(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        int maxEntries = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                return Array.Empty<LegacyBossHarmRankingEntry>();
            }

            RefreshRespawnNoLock(instance, state, now);
            var ranking = state.GetBossHarmRanking(maxEntries);
            state.ClearBossHarm();
            return ranking;
        }
    }

    public MonsterHealResult ApplyHeal(
        LegacyMonsterInstance instance,
        uint requestedHeal,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return new MonsterHealResult(
                    HealApplied: 0,
                    RemainingHitpoints: 0,
                    AlreadyDead: true);
            }

            if (requestedHeal == 0 || state.Hitpoints >= instance.MaxLife)
            {
                return new MonsterHealResult(
                    HealApplied: 0,
                    RemainingHitpoints: state.Hitpoints,
                    AlreadyDead: false);
            }

            var heal = Math.Min(requestedHeal, instance.MaxLife - state.Hitpoints);
            state.Hitpoints += heal;
            return new MonsterHealResult(
                heal,
                state.Hitpoints,
                AlreadyDead: false);
        }
    }

    public byte NextLegacyGeneratedItemQuality(int monsterFamilyId)
    {
        lock (_gate)
        {
            if (!_itemGeneratorsByMonsterId.TryGetValue(monsterFamilyId, out var state))
            {
                state = new LegacyMonsterItemGeneratorState();
                _itemGeneratorsByMonsterId.Add(monsterFamilyId, state);
            }

            return state.GenerateQuality();
        }
    }

    public uint NextLegacyDynamicMonsterUid()
    {
        lock (_gate)
        {
            for (var attempts = 0; attempts <= 100_000; attempts++)
            {
                var uid = _nextDynamicMonsterUid;
                _nextDynamicMonsterUid++;
                if (_nextDynamicMonsterUid > LegacyLastDynamicMonsterUid)
                {
                    _nextDynamicMonsterUid = LegacyFirstDynamicMonsterUid;
                }

                if (!_dynamicMonstersByUid.ContainsKey(uid))
                {
                    return uid;
                }
            }
        }

        throw new InvalidOperationException("No legacy dynamic monster UIDs are available.");
    }

    public LegacyMonsterInstance RegisterDynamicMonster(LegacyMonsterInstance instance, Monster monsterFamily)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(monsterFamily);

        lock (_gate)
        {
            _dynamicMonstersByUid[instance.Uid] = new LegacyDynamicMonsterRuntimeEntry(instance, monsterFamily);
            _states[MonsterRuntimeKey.From(instance)] = new MonsterRuntimeState(
                instance.X,
                instance.Y,
                instance.MaxLife,
                LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(instance, _legacyMonsterRespawnRandom),
                requiresObservedRemovalBeforeRespawn: true);
            return instance;
        }
    }

    public IReadOnlyList<LegacyMonsterInstance> GetDynamicMonsterLifecycleInstances(
        int mapId,
        ulong dynamicId)
    {
        lock (_gate)
        {
            return _dynamicMonstersByUid.Values
                .Where(entry =>
                    entry.Instance.MapId == mapId &&
                    entry.Instance.DynamicId == dynamicId &&
                    HasPendingMonsterLifecycleObservationNoLock(entry.Instance))
                .OrderBy(static entry => entry.Instance.Uid)
                .Select(static entry => entry.Instance)
                .ToArray();
        }
    }

    public int SuppressDynamicMonsterRespawns(int mapId, ulong dynamicId)
    {
        lock (_gate)
        {
            var suppressed = 0;
            foreach (var entry in _dynamicMonstersByUid.Values)
            {
                if (entry.Instance.MapId != mapId ||
                    entry.Instance.DynamicId != dynamicId)
                {
                    continue;
                }

                GetOrCreateNoLock(entry.Instance).RespawnSuppressed = true;
                suppressed++;
            }

            return suppressed;
        }
    }

    public int RemoveDynamicMonsters(int mapId, ulong dynamicId)
    {
        lock (_gate)
        {
            var removedUids = _dynamicMonstersByUid
                .Where(pair =>
                    pair.Value.Instance.MapId == mapId &&
                    pair.Value.Instance.DynamicId == dynamicId)
                .Select(static pair => pair.Key)
                .ToArray();

            foreach (var uid in removedUids)
            {
                var instance = _dynamicMonstersByUid[uid].Instance;
                _dynamicMonstersByUid.Remove(uid);
                _states.Remove(MonsterRuntimeKey.From(instance));
            }

            return removedUids.Length;
        }
    }

    public bool TryGetDynamicMonsterByUid(
        int mapId,
        ulong dynamicId,
        uint uid,
        out LegacyMonsterInstance instance,
        out Monster monsterFamily)
    {
        lock (_gate)
        {
            if (_dynamicMonstersByUid.TryGetValue(uid, out var entry) &&
                entry.Instance.MapId == mapId &&
                entry.Instance.DynamicId == dynamicId &&
                !IsRemovedFromMapOnDeathNoLock(entry.Instance))
            {
                instance = entry.Instance;
                monsterFamily = entry.MonsterFamily;
                return true;
            }
        }

        instance = default!;
        monsterFamily = default!;
        return false;
    }

    public bool TryGetDynamicMonsterBySpawnIdentity(
        int mapId,
        ulong dynamicId,
        int generatorId,
        int spawnIndex,
        out LegacyMonsterInstance instance,
        out Monster monsterFamily)
    {
        lock (_gate)
        {
            foreach (var entry in _dynamicMonstersByUid.Values)
            {
                if (entry.Instance.MapId == mapId &&
                    entry.Instance.DynamicId == dynamicId &&
                    entry.Instance.GeneratorId == generatorId &&
                    entry.Instance.SpawnIndex == spawnIndex)
                {
                    instance = entry.Instance;
                    monsterFamily = entry.MonsterFamily;
                    return true;
                }
            }
        }

        instance = default!;
        monsterFamily = default!;
        return false;
    }

    public IReadOnlyList<LegacyDynamicMonsterRuntimeEntry> GetVisibleDynamicMonsters(
        int mapId,
        ulong dynamicId,
        DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_dynamicMonstersByUid.Count == 0)
            {
                return [];
            }

            var result = new List<LegacyDynamicMonsterRuntimeEntry>();
            foreach (var entry in _dynamicMonstersByUid.Values)
            {
                if (entry.Instance.MapId != mapId ||
                    entry.Instance.DynamicId != dynamicId)
                {
                    continue;
                }

                var state = GetOrCreateNoLock(entry.Instance);
                RefreshRespawnNoLock(entry.Instance, state, now);
                if (state.DeadAt is not null)
                {
                    continue;
                }

                result.Add(entry with
                {
                    Instance = entry.Instance with
                    {
                        X = state.X,
                        Y = state.Y,
                        Hitpoints = state.Hitpoints,
                    },
                });
            }

            return result;
        }
    }

    private MonsterRuntimeState GetOrCreateNoLock(LegacyMonsterInstance instance)
    {
        var key = MonsterRuntimeKey.From(instance);
        if (!_states.TryGetValue(key, out var state))
        {
            state = new MonsterRuntimeState(
                instance.X,
                instance.Y,
                instance.MaxLife,
                LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(instance, _legacyMonsterRespawnRandom));
            _states.Add(key, state);
        }

        return state;
    }

    private static void RefreshRespawnNoLock(
        LegacyMonsterInstance instance,
        MonsterRuntimeState state,
        DateTimeOffset now,
        bool consumeRespawnTransition = false)
    {
        if (state.DeadAt is not { } deadAt)
        {
            ClearExpiredPoisonNoLock(state, now);
            return;
        }

        if (state.RespawnSuppressed)
        {
            return;
        }

        // MonsterRole enters Respawning from the lifecycle loop; reads must never publish that transition.
        if (!consumeRespawnTransition)
        {
            return;
        }

        if (consumeRespawnTransition &&
            state.RequiresObservedRemovalBeforeRespawn &&
            !state.DeathRemovalBroadcastSent)
        {
            return;
        }

        var schedule = LegacyMonsterLifecycleSchedule.FromDeath(deadAt, state.RespawnTimeSeconds);
        if (now < (state.NativeDeathRespawnReadyAt ?? schedule.RespawnReadyAt))
        {
            return;
        }

        state.X = instance.X;
        state.Y = instance.Y;
        state.Hitpoints = instance.MaxLife;
        state.DeadAt = null;
        state.NativeDeathRespawnReadyAt = null;
        state.PendingRespawnBroadcast = true;
        state.DeathRemovalBroadcastSent = false;
        state.LastMovedAt = null;
        state.BlackSpotExpiresAt = null;
        state.PendingBlackSpotInactiveBroadcast = false;
        state.Poison = null;
        state.TimedStatusFlags.Clear();
        state.ScarOfEarth = null;
        state.LastAttackAt = null;
        state.LastAttackTargetUid = null;
        state.TargetLock = LegacyMonsterTargetLock.ResetForRespawn();
        state.ClearBossHarm();
    }

    private static void QueueExpiredBlackSpotInactiveNoLock(
        MonsterRuntimeState state,
        DateTimeOffset now)
    {
        if (state.DeadAt is not null ||
            state.BlackSpotExpiresAt is not { } blackSpotExpiresAt ||
            now <= blackSpotExpiresAt)
        {
            return;
        }

        state.BlackSpotExpiresAt = null;
        state.PendingBlackSpotInactiveBroadcast = true;
    }

    private static void ClearExpiredPoisonNoLock(
        MonsterRuntimeState state,
        DateTimeOffset now)
    {
        if (state.Poison is { ExpiresAt: var expiresAt } &&
            now > expiresAt)
        {
            state.Poison = null;
        }
    }

    private static uint GetLegacyDirectionToward(ushort x, ushort y, ushort targetX, ushort targetY)
    {
        var deltaX = Math.Sign(targetX - x);
        var deltaY = Math.Sign(targetY - y);
        return (deltaX, deltaY) switch
        {
            (0, 1) => 0,
            (-1, 1) => 1,
            (-1, 0) => 2,
            (-1, -1) => 3,
            (0, -1) => 4,
            (1, -1) => 5,
            (1, 0) => 6,
            (1, 1) => 7,
            _ => 0,
        };
    }

    private uint GetLegacyFallbackDirection(Func<uint>? nextFallbackDirection)
    {
        return (nextFallbackDirection?.Invoke() ?? (uint)_legacyMonsterChaseFallbackRandom.Next(8)) % 8;
    }

    private static bool TryGetLegacyWalkCandidate(
        ushort x,
        ushort y,
        uint direction,
        Func<ushort, ushort, bool>? isCandidateTileValid,
        out ushort candidateX,
        out ushort candidateY)
    {
        var (deltaX, deltaY) = GetLegacyWalkDelta(direction);
        var nextX = x + deltaX;
        var nextY = y + deltaY;
        if (nextX is < 0 or > ushort.MaxValue || nextY is < 0 or > ushort.MaxValue)
        {
            candidateX = 0;
            candidateY = 0;
            return false;
        }

        candidateX = (ushort)nextX;
        candidateY = (ushort)nextY;
        if (isCandidateTileValid is not null && !isCandidateTileValid(candidateX, candidateY))
        {
            return false;
        }

        return true;
    }

    private static (int X, int Y) GetLegacyWalkDelta(uint direction)
    {
        return direction switch
        {
            0 => (0, 1),
            1 => (-1, 1),
            2 => (-1, 0),
            3 => (-1, -1),
            4 => (0, -1),
            5 => (1, -1),
            6 => (1, 0),
            7 => (1, 1),
            _ => (0, 0),
        };
    }

    private sealed class MonsterRuntimeState(
        ushort x,
        ushort y,
        uint hitpoints,
        int respawnTimeSeconds,
        bool requiresObservedRemovalBeforeRespawn = false)
    {
        public ushort X { get; set; } = x;

        public ushort Y { get; set; } = y;

        public uint Hitpoints { get; set; } = hitpoints;

        public int RespawnTimeSeconds { get; } = respawnTimeSeconds;

        public DateTimeOffset? DeadAt { get; set; }

        public DateTimeOffset? NativeDeathRespawnReadyAt { get; set; }

        public bool PendingRespawnBroadcast { get; set; }

        public bool DeathRemovalBroadcastSent { get; set; }

        public bool RespawnSuppressed { get; set; }

        public bool RemovedFromMapOnDeath { get; set; }

        public bool RequiresObservedRemovalBeforeRespawn { get; } = requiresObservedRemovalBeforeRespawn;

        public DateTimeOffset? LastMovedAt { get; set; }

        public DateTimeOffset? BlackSpotExpiresAt { get; set; }

        public bool PendingBlackSpotInactiveBroadcast { get; set; }

        public MonsterPoisonState? Poison { get; set; }

        public Dictionary<int, LegacyMonsterTimedStatusState> TimedStatusFlags { get; } = new();

        public LegacyMonsterScarOfEarthState? ScarOfEarth { get; set; }

        public DateTimeOffset? LastAttackAt { get; set; }

        public uint? LastAttackTargetUid { get; set; }

        public LegacyMonsterTargetLockState TargetLock { get; set; } = LegacyMonsterTargetLockState.Empty;

        private readonly Dictionary<uint, BossHarmContributor> _bossHarmByPlayerUid = new();

        public void RecordBossHarm(uint playerUid, string playerName, uint damage)
        {
            if (_bossHarmByPlayerUid.TryGetValue(playerUid, out var contributor))
            {
                contributor.Damage += damage;
                contributor.PlayerName = playerName;
                return;
            }

            _bossHarmByPlayerUid.Add(playerUid, new BossHarmContributor(playerUid, playerName, damage));
        }

        public IReadOnlyList<LegacyBossHarmRankingEntry> GetBossHarmRanking(int maxEntries)
        {
            if (_bossHarmByPlayerUid.Count == 0)
            {
                return Array.Empty<LegacyBossHarmRankingEntry>();
            }

            return _bossHarmByPlayerUid.Values
                .OrderByDescending(static contributor => contributor.Damage)
                .ThenBy(static contributor => contributor.PlayerUid)
                .Take(maxEntries)
                .Select(static (contributor, index) => new LegacyBossHarmRankingEntry(
                    contributor.PlayerUid,
                    contributor.PlayerName,
                    contributor.Damage,
                    Rank: index + 1))
                .ToArray();
        }

        public void ClearBossHarm()
        {
            _bossHarmByPlayerUid.Clear();
        }
    }

    private sealed class BossHarmContributor(uint playerUid, string playerName, ulong damage)
    {
        public uint PlayerUid { get; } = playerUid;

        public string PlayerName { get; set; } = playerName;

        public ulong Damage { get; set; } = damage;
    }

    private sealed class LegacyMonsterItemGeneratorState
    {
        private int _refinedCount;
        private int _uniqueCount;
        private int _eliteCount;
        private int _superCount;

        public byte GenerateQuality()
        {
            if (AdvanceRateWatcher(ref _refinedCount, tick: 10))
            {
                return 6;
            }

            if (AdvanceRateWatcher(ref _uniqueCount, tick: 20))
            {
                return 7;
            }

            if (AdvanceRateWatcher(ref _eliteCount, tick: 25))
            {
                return 8;
            }

            if (AdvanceRateWatcher(ref _superCount, tick: 30))
            {
                return 9;
            }

            return 3;
        }

        private static bool AdvanceRateWatcher(ref int count, int tick)
        {
            count++;
            if (count != tick)
            {
                return false;
            }

            count = 0;
            return true;
        }
    }

    private readonly record struct MonsterRuntimeKey(uint Uid, int MapId, uint DynamicId, int GeneratorId, int SpawnIndex)
    {
        public static MonsterRuntimeKey From(LegacyMonsterInstance instance)
        {
            return new MonsterRuntimeKey(
                instance.Uid,
                instance.MapId,
                instance.DynamicId,
                instance.GeneratorId,
                instance.SpawnIndex);
        }
    }
}

public readonly record struct MonsterPoisonState(
    byte PoisonLevel,
    DateTimeOffset ExpiresAt,
    int StatusFlag);

public readonly record struct MonsterPoisonResult(
    bool Applied,
    bool AlreadyDead,
    bool BossImmune,
    MonsterPoisonState? Poison);

public readonly record struct MonsterDamageResult(
    uint DamageApplied,
    uint RemainingHitpoints,
    bool Killed,
    bool AlreadyDead,
    uint RequestedDamage = 0);

public readonly record struct MonsterHealResult(
    uint HealApplied,
    uint RemainingHitpoints,
    bool AlreadyDead);

public readonly record struct LegacyBossHarmRankingEntry(
    uint PlayerUid,
    string PlayerName,
    ulong Damage,
    int Rank);

public readonly record struct LegacyDynamicMonsterRuntimeEntry(
    LegacyMonsterInstance Instance,
    Monster MonsterFamily);
