using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    internal MonsterDamageResult ApplyLegacyFamily8418ReceiveDamage(
        LegacyMonsterInstance instance, uint requestedDamage, DateTimeOffset now,
        uint? attackerUid, string? attackerName) =>
        ApplyDamage(instance, requestedDamage, now, attackerUid, attackerName,
            deferNativeDeathPrefix: true);

    internal bool BeginLegacyFamily8418DeathPrefix(LegacyMonsterInstance instance, DateTimeOffset now)
    {
        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            // Native RespawnStamp is set before the BlackSpot send, not from the later DeadStamp.
            state.NativeDeathRespawnReadyAt =
                LegacyMonsterLifecycleSchedule.FromDeath(now, state.RespawnTimeSeconds).RespawnReadyAt;
            return state.BlackSpotExpiresAt is not null;
        }
    }

    internal bool CommitLegacyFamily8418DeathHitpointsAndFlags(
        LegacyMonsterInstance instance, DateTimeOffset now, out ulong[] statusFlags)
    {
        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            // The inactive BlackSpot send has returned before this source-ordered commit.
            state.BlackSpotExpiresAt = null;
            state.PendingBlackSpotInactiveBroadcast = false;
            state.Poison = null;
            // Native GetClear snapshots/removes only RemoveOnDead=true entries.
            // Do not expire survivors here: GetClear does not inspect their timers.
            foreach (var key in state.TimedStatusFlags.Values
                         .Where(status => status.RemoveOnDead).Select(status => status.StatusFlag).ToArray())
                state.TimedStatusFlags.Remove(key);
            state.ScarOfEarth = null;
            state.Hitpoints = 0;
            var addedDead = !state.TimedStatusFlags.ContainsKey(5);
            if (addedDead)
                state.TimedStatusFlags[5] = new LegacyMonsterTimedStatusState(
                    5, now.AddSeconds(2_592_000), RemoveOnDead: true);
            // Existing target bookkeeping, not additional source rewards or RNG.
            state.PendingRespawnBroadcast = false;
            state.DeathRemovalBroadcastSent = false;
            state.TargetLock = LegacyMonsterTargetLockState.Empty;
            state.LastAttackAt = null;
            state.LastAttackTargetUid = null;
            statusFlags = [];
            if (addedDead)
            {
                statusFlags = new ulong[10];
                foreach (var status in state.TimedStatusFlags.Values)
                    SetStatusFlag(statusFlags, status.StatusFlag);
            }
            // Native MonsterRole.AddFlag calls UpdateFlagOffset ONLY if Dead was absent.
            return addedDead;
        }
    }

    internal void StampLegacyFamily8418Death(LegacyMonsterInstance instance, DateTimeOffset now)
    {
        lock (_gate)
        {
            // Native DeadStamp follows AddFlag, including its already-present/no-publication return.
            GetOrCreateNoLock(instance).DeadAt = now;
        }
    }

    internal void CompleteLegacyMonsterDeathRemoval(LegacyMonsterInstance instance)
    {
        if (!instance.RemoveOnDead) return;
        lock (_gate)
        {
            if (_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) &&
                state.DeadAt is not null)
            {
                // Native RemoveOnDead is after the family8418 whole-Dead return and Death send.
                state.RemovedFromMapOnDeath = true;
                state.RespawnSuppressed = true;
            }
        }
    }
}
