namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    public LegacyMonsterTimedStatusApplyResult ApplyTimedStatusFlag(
        LegacyMonsterInstance instance,
        int statusFlag,
        TimeSpan duration,
        DateTimeOffset now,
        bool refreshIfActive = true)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(statusFlag);
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Status duration cannot be negative.");
        }

        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null)
            {
                return new LegacyMonsterTimedStatusApplyResult(
                    Applied: false,
                    AlreadyDead: true,
                    AlreadyActive: false,
                    Status: null);
            }

            ClearExpiredTimedStatusFlagsNoLock(state, now);

            if (state.TimedStatusFlags.TryGetValue(statusFlag, out var active) &&
                !refreshIfActive)
            {
                return new LegacyMonsterTimedStatusApplyResult(
                    Applied: false,
                    AlreadyDead: false,
                    AlreadyActive: true,
                    active);
            }

            var status = new LegacyMonsterTimedStatusState(
                statusFlag,
                now + duration,
                RemoveOnDead: true);
            state.TimedStatusFlags[statusFlag] = status;
            return new LegacyMonsterTimedStatusApplyResult(
                Applied: true,
                AlreadyDead: false,
                AlreadyActive: false,
                status);
        }
    }

    public bool TryGetTimedStatusFlag(
        LegacyMonsterInstance instance,
        int statusFlag,
        DateTimeOffset now,
        out LegacyMonsterTimedStatusState status)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(statusFlag);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                status = default;
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null ||
                !state.TimedStatusFlags.TryGetValue(statusFlag, out status) ||
                now > status.ExpiresAt)
            {
                status = default;
                return false;
            }

            return true;
        }
    }

    public ulong[] CreateStatusFlagVector(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        int length)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                return new ulong[length];
            }

            RefreshRespawnNoLock(instance, state, now);
            return state.DeadAt is null
                ? CreateStatusFlagVectorNoLock(state, length, now)
                : new ulong[length];
        }
    }

    public bool TryExpireTimedStatusFlags(
        LegacyMonsterInstance instance,
        DateTimeOffset now,
        int vectorLength,
        out ulong[] statusFlags)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentOutOfRangeException.ThrowIfNegative(vectorLength);

        lock (_gate)
        {
            if (!_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                statusFlags = [];
                return false;
            }

            RefreshRespawnNoLock(instance, state, now);
            if (state.DeadAt is not null || state.TimedStatusFlags.Count == 0)
            {
                statusFlags = [];
                return false;
            }

            var countBeforeExpiry = state.TimedStatusFlags.Count;
            ClearExpiredTimedStatusFlagsNoLock(state, now);
            if (state.TimedStatusFlags.Count == countBeforeExpiry)
            {
                statusFlags = [];
                return false;
            }

            statusFlags = CreateStatusFlagVectorNoLock(state, vectorLength, now);
            return true;
        }
    }

    private static ulong[] CreateStatusFlagVectorNoLock(
        MonsterRuntimeState state,
        int length,
        DateTimeOffset now)
    {
        var values = new ulong[length];
        if (state.Poison is { } poison)
        {
            SetStatusFlag(values, poison.StatusFlag);
        }

        foreach (var status in state.TimedStatusFlags.Values)
        {
            if (now <= status.ExpiresAt)
            {
                SetStatusFlag(values, status.StatusFlag);
            }
        }

        return values;
    }

    private static void SetStatusFlag(ulong[] values, int statusFlag)
    {
        var wordIndex = statusFlag / 32;
        if (wordIndex < values.Length)
        {
            values[wordIndex] |= 1UL << (statusFlag % 32);
        }
    }

    private static void ClearExpiredTimedStatusFlagsNoLock(
        MonsterRuntimeState state,
        DateTimeOffset now)
    {
        if (state.TimedStatusFlags.Count == 0)
        {
            return;
        }

        foreach (var statusFlag in state.TimedStatusFlags
                     .Where(entry => now > entry.Value.ExpiresAt)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            state.TimedStatusFlags.Remove(statusFlag);
        }
    }
}

public readonly record struct LegacyMonsterTimedStatusState(
    int StatusFlag,
    DateTimeOffset ExpiresAt,
    bool RemoveOnDead);

public readonly record struct LegacyMonsterTimedStatusApplyResult(
    bool Applied,
    bool AlreadyDead,
    bool AlreadyActive,
    LegacyMonsterTimedStatusState? Status);
