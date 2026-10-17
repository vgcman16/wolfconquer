namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    public LegacyMonsterScarOfEarthApplyResult ApplyScarOfEarth(
        LegacyMonsterInstance instance,
        int statusFlag,
        TimeSpan duration,
        TimeSpan invokeInterval,
        DateTimeOffset now,
        uint casterUid,
        ushort spellId,
        byte spellLevel,
        uint damage)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(statusFlag);
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Status duration cannot be negative.");
        }

        if (invokeInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(invokeInterval),
                invokeInterval,
                "Invoke interval must be positive.");
        }

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return new LegacyMonsterScarOfEarthApplyResult(
                    Applied: false,
                    AlreadyDead: true,
                    AlreadyActive: false,
                    State: null);
            }

            var alreadyActive = state.TimedStatusFlags.ContainsKey(statusFlag);
            if (!alreadyActive)
            {
                var activeStatus = new LegacyMonsterTimedStatusState(
                    statusFlag,
                    now + duration,
                    RemoveOnDead: true);
                state.TimedStatusFlags[statusFlag] = activeStatus;
            }

            var nextInvokeAtUtc = alreadyActive && state.ScarOfEarth is { } current
                ? current.NextInvokeAtUtc
                : now + invokeInterval;
            var effectiveInvokeInterval = alreadyActive && state.ScarOfEarth is { } existing
                ? existing.InvokeInterval
                : invokeInterval;
            var scar = new LegacyMonsterScarOfEarthState(
                nextInvokeAtUtc,
                effectiveInvokeInterval,
                casterUid,
                spellId,
                spellLevel,
                damage);
            state.ScarOfEarth = scar;

            return new LegacyMonsterScarOfEarthApplyResult(
                Applied: !alreadyActive,
                AlreadyDead: false,
                AlreadyActive: alreadyActive,
                State: scar);
        }
    }

    public LegacyMonsterScarOfEarthTickResult ConsumeScarOfEarthTick(
        LegacyMonsterInstance instance,
        int statusFlag,
        DateTimeOffset now,
        int vectorLength)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(statusFlag);
        ArgumentOutOfRangeException.ThrowIfNegative(vectorLength);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                return LegacyMonsterScarOfEarthTickResult.None;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                state.ScarOfEarth = null;
                return LegacyMonsterScarOfEarthTickResult.None;
            }

            if (!state.TimedStatusFlags.TryGetValue(statusFlag, out var status))
            {
                state.ScarOfEarth = null;
                return LegacyMonsterScarOfEarthTickResult.None;
            }

            // PoolProcesses.cs uses Expire(...) followed by else-if ScarofEarth.
            if (now >= status.ExpiresAt)
            {
                state.TimedStatusFlags.Remove(statusFlag);
                state.ScarOfEarth = null;
                return new LegacyMonsterScarOfEarthTickResult(
                    Expired: true,
                    Due: false,
                    StatusFlags: CreateStatusFlagVectorNoLock(state, vectorLength, now),
                    State: null);
            }

            if (state.ScarOfEarth is not { } scar || now < scar.NextInvokeAtUtc)
            {
                return LegacyMonsterScarOfEarthTickResult.None;
            }

            var due = scar with { NextInvokeAtUtc = now + scar.InvokeInterval };
            state.ScarOfEarth = due;
            return new LegacyMonsterScarOfEarthTickResult(
                Expired: false,
                Due: true,
                StatusFlags: [],
                State: due);
        }
    }
}

public readonly record struct LegacyMonsterScarOfEarthState(
    DateTimeOffset NextInvokeAtUtc,
    TimeSpan InvokeInterval,
    uint CasterUid,
    ushort SpellId,
    byte SpellLevel,
    uint Damage);

public readonly record struct LegacyMonsterScarOfEarthApplyResult(
    bool Applied,
    bool AlreadyDead,
    bool AlreadyActive,
    LegacyMonsterScarOfEarthState? State);

public readonly record struct LegacyMonsterScarOfEarthTickResult(
    bool Expired,
    bool Due,
    ulong[] StatusFlags,
    LegacyMonsterScarOfEarthState? State)
{
    public static LegacyMonsterScarOfEarthTickResult None { get; } = new(
        Expired: false,
        Due: false,
        StatusFlags: [],
        State: null);
}
