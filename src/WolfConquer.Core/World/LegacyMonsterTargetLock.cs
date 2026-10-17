namespace WolfConquer.Core.World;

public static class LegacyMonsterTargetLock
{
    public static LegacyMonsterTargetLockState ResetForRespawn()
    {
        return LegacyMonsterTargetLockState.Empty;
    }

    public static LegacyMonsterTargetLockResult AdvanceAttack(
        LegacyMonsterTargetLockState state,
        LegacyMonsterTargetSnapshot? target,
        int attackRange,
        bool boss,
        uint hitpoints,
        DateTimeOffset now,
        TimeSpan attackInterval)
    {
        ValidateRange(attackRange, nameof(attackRange), "Attack range cannot be negative.");
        ValidateAttackInterval(attackInterval);

        var effectiveAttackRange = ResolveEffectiveAttackRange(
            attackRange,
            boss,
            hitpoints,
            targetAlreadyAcquired: true);

        return DecideNextActionCore(
            state,
            target,
            LegacyMonsterTargetLockLifecycle.Alive,
            effectiveAttackRange,
            effectiveAttackRange,
            now,
            attackInterval);
    }

    public static LegacyMonsterTargetLockResult DecideNextAction(
        LegacyMonsterTargetLockState state,
        LegacyMonsterTargetSnapshot? target,
        LegacyMonsterTargetLockLifecycle lifecycle,
        int attackRange,
        int scanRange,
        bool boss,
        uint hitpoints,
        DateTimeOffset now,
        TimeSpan attackInterval)
    {
        ValidateRange(attackRange, nameof(attackRange), "Attack range cannot be negative.");
        ValidateRange(scanRange, nameof(scanRange), "Scan range cannot be negative.");
        ValidateAttackInterval(attackInterval);

        var targetAlreadyAcquired = target is { } snapshot &&
            state.LastTargetUid == snapshot.Uid;
        var effectiveAttackRange = ResolveEffectiveAttackRange(
            attackRange,
            boss,
            hitpoints,
            targetAlreadyAcquired);

        return DecideNextActionCore(
            state,
            target,
            lifecycle,
            effectiveAttackRange,
            Math.Max(scanRange, effectiveAttackRange),
            now,
            attackInterval);
    }

    private static LegacyMonsterTargetLockResult DecideNextActionCore(
        LegacyMonsterTargetLockState state,
        LegacyMonsterTargetSnapshot? target,
        LegacyMonsterTargetLockLifecycle lifecycle,
        int effectiveAttackRange,
        int effectiveScanRange,
        DateTimeOffset now,
        TimeSpan attackInterval)
    {
        if (lifecycle is LegacyMonsterTargetLockLifecycle.Dead or LegacyMonsterTargetLockLifecycle.Respawning)
        {
            return new LegacyMonsterTargetLockResult(
                LegacyMonsterTargetLockState.Empty,
                LegacyMonsterTargetLockAction.ResetTargetLock,
                AttackDue: false,
                effectiveAttackRange,
                effectiveScanRange);
        }

        if (lifecycle != LegacyMonsterTargetLockLifecycle.Alive)
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unknown monster target-lock lifecycle.");
        }

        if (target is not { } snapshot ||
            !IsValidTargetForScan(snapshot, effectiveScanRange))
        {
            return new LegacyMonsterTargetLockResult(
                state with { LastTargetUid = null },
                LegacyMonsterTargetLockAction.SearchTarget,
                AttackDue: false,
                effectiveAttackRange,
                effectiveScanRange);
        }

        var retained = state with { LastTargetUid = snapshot.Uid };
        if (snapshot.Distance > effectiveAttackRange)
        {
            return new LegacyMonsterTargetLockResult(
                retained,
                LegacyMonsterTargetLockAction.RetainTarget,
                AttackDue: false,
                effectiveAttackRange,
                effectiveScanRange);
        }

        if (retained.LastAttackAt is { } lastAttackAt &&
            now <= lastAttackAt + attackInterval)
        {
            return new LegacyMonsterTargetLockResult(
                retained,
                LegacyMonsterTargetLockAction.WaitForAttackInterval,
                AttackDue: false,
                effectiveAttackRange,
                effectiveScanRange);
        }

        return new LegacyMonsterTargetLockResult(
            retained with { LastAttackAt = now },
            LegacyMonsterTargetLockAction.Attack,
            AttackDue: true,
            effectiveAttackRange,
            effectiveScanRange);
    }

    private static bool IsValidTargetForScan(
        LegacyMonsterTargetSnapshot target,
        int effectiveScanRange)
    {
        return target.Distance <= effectiveScanRange &&
            target.Alive &&
            !target.Flying &&
            target.SocketAlive;
    }

    private static int ResolveEffectiveAttackRange(
        int attackRange,
        bool boss,
        uint hitpoints,
        bool targetAlreadyAcquired)
    {
        var usesLegacyBossRange = boss &&
            (hitpoints > 2_000_000U ||
                (targetAlreadyAcquired && hitpoints >= 2_000_000U));

        return usesLegacyBossRange
            ? Math.Max(attackRange, 18)
            : attackRange;
    }

    private static void ValidateRange(
        int range,
        string parameterName,
        string message)
    {
        if (range < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, range, message);
        }
    }

    private static void ValidateAttackInterval(TimeSpan attackInterval)
    {
        if (attackInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(attackInterval), attackInterval, "Attack interval cannot be negative.");
        }
    }
}

public readonly record struct LegacyMonsterTargetLockState(
    uint? LastTargetUid,
    DateTimeOffset? LastAttackAt)
{
    public static LegacyMonsterTargetLockState Empty { get; } = new(null, null);
}

public readonly record struct LegacyMonsterTargetSnapshot(
    uint Uid,
    int Distance,
    bool Alive,
    bool Flying,
    bool SocketAlive);

public readonly record struct LegacyMonsterTargetLockResult(
    LegacyMonsterTargetLockState State,
    LegacyMonsterTargetLockAction Action,
    bool AttackDue,
    int EffectiveAttackRange,
    int EffectiveScanRange)
{
    public LegacyMonsterTargetLockResult(
        LegacyMonsterTargetLockState State,
        LegacyMonsterTargetLockAction Action,
        bool AttackDue,
        int EffectiveAttackRange)
        : this(State, Action, AttackDue, EffectiveAttackRange, EffectiveAttackRange)
    {
    }
}

public enum LegacyMonsterTargetLockLifecycle
{
    Alive,
    Dead,
    Respawning,
}

public enum LegacyMonsterTargetLockAction
{
    SearchTarget,
    RetainTarget,
    WaitForAttackInterval,
    Attack,
    ResetTargetLock,
}
