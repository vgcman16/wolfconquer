using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MonsterRuntimeStateStoreTests
{
    [Fact]
    public void ApplyDamageTracksDeathAndRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var first = store.ApplyDamage(monster, requestedDamage: 40, now);
        Assert.Equal(40U, first.RequestedDamage);
        Assert.Equal(40U, first.DamageApplied);
        Assert.Equal(60U, first.RemainingHitpoints);
        Assert.False(first.Killed);

        var second = store.ApplyDamage(monster, requestedDamage: 70, now.AddSeconds(1));
        Assert.Equal(70U, second.RequestedDamage);
        Assert.Equal(60U, second.DamageApplied);
        Assert.Equal(0U, second.RemainingHitpoints);
        Assert.True(second.Killed);
        Assert.True(second.RequestedDamage > second.DamageApplied);

        Assert.False(store.TryGetVisibleInstance(monster, now.AddSeconds(8), out _));
        Assert.False(store.TryGetVisibleInstance(monster, now.AddSeconds(10), out _));
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(9), out _));
        Assert.False(store.TryGetVisibleInstance(monster, now.AddSeconds(11), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out var respawned));
        Assert.Equal(100U, respawned.Hitpoints);
        Assert.True(store.TryGetVisibleInstance(monster, now.AddSeconds(11), out _));
    }

    [Fact]
    public void TryConsumeRespawnedInstanceReturnsOneShotRespawnTransition()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now);
        Assert.True(kill.Killed);

        Assert.False(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(9), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(10), out var respawned));
        Assert.Equal(100U, respawned.Hitpoints);
        Assert.False(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out _));
        Assert.True(store.TryGetVisibleInstance(monster, now.AddSeconds(11), out var visible));
        Assert.Equal(100U, visible.Hitpoints);
    }

    [Fact]
    public void TryConsumeDeathRemovalInstanceReturnsOneShotLegacyFadeRemoval()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now);
        Assert.True(kill.Killed);

        Assert.False(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(7), out _));
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(8), out var removed));
        Assert.Equal(monster.Uid, removed.Uid);
        Assert.Equal(0U, removed.Hitpoints);
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(9), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(10), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void TryConsumeRespawnedInstanceUsesGeneratorRestSecondsForSourceBackedIds(int restSeconds)
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now);
        Assert.True(kill.Killed);

        Assert.False(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(7), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(8), out var respawned));
        Assert.Equal(100U, respawned.Hitpoints);
    }

    [Fact]
    public void TryConsumeRespawnedInstanceUsesFamilyRespawnTimeForNormalMonster()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 76);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now);
        Assert.True(kill.Killed);

        Assert.False(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(17), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(28), out var respawned));
        Assert.Equal(100U, respawned.Hitpoints);
    }

    [Fact]
    public void ApplyDamageIgnoresDeadMonsterUntilRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 10, restSeconds: 5, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var kill = store.ApplyDamage(monster, requestedDamage: 10, now);
        var whileDead = store.ApplyDamage(monster, requestedDamage: 10, now.AddSeconds(1));

        Assert.True(kill.Killed);
        Assert.True(whileDead.AlreadyDead);
        Assert.Equal(0U, whileDead.DamageApplied);
    }

    [Fact]
    public void ApplyDamageTracksBossHarmRankingForNonLethalHits()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Monster(hitpoints: 1_000, restSeconds: 20, monsterId: 4171, boss: true);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        store.ApplyDamage(boss, requestedDamage: 100, now, attackerUid: 1_001, attackerName: "TestSrv");
        store.ApplyDamage(boss, requestedDamage: 200, now.AddSeconds(1), attackerUid: 1_002, attackerName: "Ninja");
        store.ApplyDamage(boss, requestedDamage: 150, now.AddSeconds(2), attackerUid: 1_001, attackerName: "RayzoPrime");

        var death = store.ApplyDamage(
            boss,
            requestedDamage: 1_000,
            now.AddSeconds(3),
            attackerUid: 1_003,
            attackerName: "LastHit");
        Assert.True(death.Killed);

        var ranking = store.GetBossHarmRanking(boss, now.AddSeconds(3));
        Assert.Collection(
            ranking,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal(1_001U, first.PlayerUid);
                Assert.Equal("RayzoPrime", first.PlayerName);
                Assert.Equal(250UL, first.Damage);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal(1_002U, second.PlayerUid);
                Assert.Equal("Ninja", second.PlayerName);
                Assert.Equal(200UL, second.Damage);
            });
        Assert.DoesNotContain(ranking, entry => entry.PlayerUid == 1_003U);
    }

    [Fact]
    public void ApplyDamageDoesNotTrackHarmRankingForNormalMonster()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 1_000, restSeconds: 20, monsterId: 76);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        store.ApplyDamage(monster, requestedDamage: 100, now, attackerUid: 1_001, attackerName: "TestSrv");

        Assert.Empty(store.GetBossHarmRanking(monster, now));
    }

    [Fact]
    public void GetBossHarmRankingReturnsTopEntriesWithSourceStyleRanks()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Monster(hitpoints: 10_000, restSeconds: 20, monsterId: 4171, boss: true);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        for (uint index = 1; index <= 11; index++)
        {
            store.ApplyDamage(
                boss,
                requestedDamage: index * 10,
                now.AddSeconds(index),
                attackerUid: 1_000 + index,
                attackerName: $"Player{index}");
        }

        var ranking = store.GetBossHarmRanking(boss, now.AddSeconds(12), maxEntries: 9);

        Assert.Equal(9, ranking.Count);
        Assert.Equal(1_011U, ranking[0].PlayerUid);
        Assert.Equal(110UL, ranking[0].Damage);
        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(1_003U, ranking[8].PlayerUid);
        Assert.Equal(30UL, ranking[8].Damage);
        Assert.Equal(9, ranking[8].Rank);
    }

    [Fact]
    public void ConsumeBossHarmRankingClearsStoredContributors()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Monster(hitpoints: 1_000, restSeconds: 20, monsterId: 4171, boss: true);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        store.ApplyDamage(boss, requestedDamage: 100, now, attackerUid: 1_001, attackerName: "TestSrv");

        var ranking = store.ConsumeBossHarmRanking(boss, now.AddSeconds(1));

        Assert.Single(ranking);
        Assert.Empty(store.GetBossHarmRanking(boss, now.AddSeconds(1)));
    }

    [Fact]
    public void RespawnClearsBossHarmRanking()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Monster(hitpoints: 1_000, restSeconds: 2, monsterId: 2700, boss: true);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        store.ApplyDamage(boss, requestedDamage: 100, now, attackerUid: 1_001, attackerName: "TestSrv");
        var death = store.ApplyDamage(boss, requestedDamage: 1_000, now.AddSeconds(1));
        Assert.True(death.Killed);
        Assert.NotEmpty(store.GetBossHarmRanking(boss, now.AddSeconds(2)));

        Assert.True(store.TryConsumeRespawnedInstance(boss, now.AddSeconds(11), out _));

        Assert.Empty(store.GetBossHarmRanking(boss, now.AddSeconds(11)));
    }

    [Fact]
    public void TryMarkAttackDueUsesSingleSourceAttackSpeedPerMonster()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var attackInterval = TimeSpan.FromMilliseconds(500);

        Assert.True(store.TryMarkAttackDue(monster, targetUid: 1_001, now, attackInterval));
        Assert.True(store.TryGetLastAttackTargetUid(monster, now, out var targetUid));
        Assert.Equal(1_001U, targetUid);

        Assert.False(store.TryMarkAttackDue(
            monster,
            targetUid: 2_002,
            now.AddMilliseconds(499),
            attackInterval));
        Assert.False(store.TryMarkAttackDue(
            monster,
            targetUid: 2_002,
            now.AddMilliseconds(500),
            attackInterval));

        Assert.True(store.TryGetLastAttackTargetUid(monster, now.AddMilliseconds(500), out targetUid));
        Assert.Equal(1_001U, targetUid);

        Assert.True(store.TryMarkAttackDue(
            monster,
            targetUid: 2_002,
            now.AddMilliseconds(501),
            attackInterval));
        Assert.True(store.TryGetLastAttackTargetUid(monster, now.AddMilliseconds(501), out targetUid));
        Assert.Equal(2_002U, targetUid);
    }

    [Fact]
    public void RespawnResetsAttackCadenceAndLastAttackTarget()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var attackInterval = TimeSpan.FromMinutes(10);

        Assert.True(store.TryMarkAttackDue(monster, targetUid: 1_001, now, attackInterval));
        Assert.True(store.TryGetLastAttackTargetUid(monster, now, out var targetUid));
        Assert.Equal(1_001U, targetUid);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);

        Assert.False(store.TryGetLastAttackTargetUid(monster, now.AddSeconds(2), out _));
        Assert.False(store.TryMarkAttackDue(monster, targetUid: 2_002, now.AddSeconds(2), attackInterval));

        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out _));
        Assert.False(store.TryGetLastAttackTargetUid(monster, now.AddSeconds(11), out _));
        Assert.True(store.TryMarkAttackDue(monster, targetUid: 2_002, now.AddSeconds(11), attackInterval));
    }

    [Fact]
    public void AdvanceTargetLockPersistsFirstAttackAndWaitsOnImmediateSecondDecision()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var attackInterval = TimeSpan.FromMilliseconds(500);

        var first = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now,
            attackInterval);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, first.Action);
        Assert.True(first.AttackDue);
        Assert.Equal(1_001U, first.State.LastTargetUid);
        Assert.Equal(now, first.State.LastAttackAt);

        var immediate = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddMilliseconds(1),
            attackInterval);

        Assert.Equal(LegacyMonsterTargetLockAction.WaitForAttackInterval, immediate.Action);
        Assert.False(immediate.AttackDue);
        Assert.Equal(1_001U, immediate.State.LastTargetUid);
        Assert.Equal(now, immediate.State.LastAttackAt);
    }

    [Fact]
    public void AdvanceTargetLockClearsTargetWhenOutOfScanOrNoTarget()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var attackInterval = TimeSpan.FromMilliseconds(500);

        var first = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now,
            attackInterval);
        Assert.True(first.AttackDue);

        var outOfScan = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 19),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddSeconds(1),
            attackInterval);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, outOfScan.Action);
        Assert.False(outOfScan.AttackDue);
        Assert.Null(outOfScan.State.LastTargetUid);
        Assert.Equal(now, outOfScan.State.LastAttackAt);

        var reacquired = store.AdvanceTargetLock(
            monster,
            Target(uid: 2_002, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddSeconds(2),
            attackInterval);
        Assert.True(reacquired.AttackDue);
        Assert.Equal(2_002U, reacquired.State.LastTargetUid);

        var noTarget = store.AdvanceTargetLock(
            monster,
            target: null,
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddSeconds(3),
            attackInterval);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, noTarget.Action);
        Assert.False(noTarget.AttackDue);
        Assert.Null(noTarget.State.LastTargetUid);
        Assert.Equal(now.AddSeconds(2), noTarget.State.LastAttackAt);
    }

    [Fact]
    public void DeathAndRespawnResetTargetLockStateAndAllowNewTarget()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var attackInterval = TimeSpan.FromMinutes(10);

        var first = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now,
            attackInterval);
        Assert.True(first.AttackDue);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);

        var whileDead = store.AdvanceTargetLock(
            monster,
            Target(uid: 1_001, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddSeconds(2),
            attackInterval);
        Assert.Equal(LegacyMonsterTargetLockAction.ResetTargetLock, whileDead.Action);
        Assert.False(whileDead.AttackDue);
        Assert.Equal(LegacyMonsterTargetLockState.Empty, whileDead.State);

        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out _));

        var afterRespawn = store.AdvanceTargetLock(
            monster,
            Target(uid: 2_002, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            now.AddSeconds(11),
            attackInterval);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, afterRespawn.Action);
        Assert.True(afterRespawn.AttackDue);
        Assert.Equal(2_002U, afterRespawn.State.LastTargetUid);
        Assert.Equal(now.AddSeconds(11), afterRespawn.State.LastAttackAt);
    }

    [Fact]
    public void TryMarkBlackSpotUntilTracksAliveMonsterUntilSourceExpiryBoundary()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var expiresAt = now.AddSeconds(5);

        Assert.True(store.TryMarkBlackSpotUntil(monster, now, expiresAt));

        Assert.True(store.IsBlackSpotted(monster, now.AddSeconds(4)));
        Assert.True(store.IsBlackSpotted(monster, expiresAt));
        Assert.False(store.IsBlackSpotted(monster, expiresAt.AddTicks(1)));
    }

    [Fact]
    public void TryConsumeBlackSpotInactiveInstanceUsesSourceExpiryBoundaryAndClearsOnce()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var expiresAt = now.AddSeconds(5);

        Assert.True(store.TryMarkBlackSpotUntil(monster, now, expiresAt));
        var damage = store.ApplyDamage(monster, requestedDamage: 35, now.AddSeconds(2));
        Assert.Equal(65U, damage.RemainingHitpoints);

        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, now.AddSeconds(4), out _));
        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, expiresAt, out _));
        Assert.True(store.TryConsumeBlackSpotInactiveInstance(monster, expiresAt.AddTicks(1), out var inactive));

        Assert.Equal(monster.Uid, inactive.Uid);
        Assert.Equal(65U, inactive.Hitpoints);
        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, expiresAt.AddTicks(2), out _));
        Assert.False(store.IsBlackSpotted(monster, expiresAt.AddTicks(2)));
    }

    [Fact]
    public void TryConsumeBlackSpotInactiveInstanceConsumesOnceWhenBlackSpottedMonsterDiesBeforeExpiry()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var expiresAt = now.AddSeconds(30);

        Assert.True(store.TryMarkBlackSpotUntil(monster, now, expiresAt));
        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);

        Assert.True(store.TryConsumeBlackSpotInactiveInstance(monster, now.AddSeconds(2), out var inactive));
        Assert.Equal(monster.Uid, inactive.Uid);
        Assert.Equal(0U, inactive.Hitpoints);
        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, now.AddSeconds(3), out _));

        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out var respawned));
        Assert.Equal(monster.Uid, respawned.Uid);
        Assert.Equal(100U, respawned.Hitpoints);
    }

    [Fact]
    public void BlackSpotCannotMarkOrQueryDeadMonsterUntilRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var expiresAt = now.AddSeconds(30);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now);
        Assert.True(kill.Killed);

        Assert.False(store.TryMarkBlackSpotUntil(monster, now.AddSeconds(9), expiresAt));
        Assert.False(store.IsBlackSpotted(monster, now.AddSeconds(9)));

        Assert.True(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(8), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(10), out _));
        Assert.True(store.TryMarkBlackSpotUntil(monster, now.AddSeconds(10), expiresAt));
        Assert.True(store.IsBlackSpotted(monster, now.AddSeconds(10)));
    }

    [Fact]
    public void RespawnResetsBlackSpotState()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        Assert.True(store.TryMarkBlackSpotUntil(monster, now, now.AddSeconds(30)));

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);

        Assert.False(store.IsBlackSpotted(monster, now.AddSeconds(2)));
        Assert.False(store.IsBlackSpotted(monster, now.AddSeconds(11)));
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, now.AddSeconds(9), out _));
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out _));
        Assert.True(store.TryGetVisibleInstance(monster, now.AddSeconds(11), out var visible));
        Assert.Equal(100U, visible.Hitpoints);
    }

    [Fact]
    public void ApplyPoisonTracksSourceFlagLevelAndExpiry()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var expiresAt = now.AddSeconds(5);

        var result = store.ApplyPoison(monster, poisonLevel: 3, TimeSpan.FromSeconds(5), now);

        Assert.True(result.Applied);
        Assert.False(result.AlreadyDead);
        Assert.False(result.BossImmune);
        Assert.True(result.Poison.HasValue);
        Assert.Equal(3, result.Poison.Value.PoisonLevel);
        Assert.Equal(expiresAt, result.Poison.Value.ExpiresAt);
        Assert.Equal(MonsterRuntimeStateStore.LegacyPoisonedStatusFlag, result.Poison.Value.StatusFlag);

        Assert.True(store.TryGetPoisonState(monster, now.AddSeconds(4), out var active));
        Assert.Equal(3, active.PoisonLevel);
        Assert.Equal(expiresAt, active.ExpiresAt);
        Assert.Equal(MonsterRuntimeStateStore.LegacyPoisonedStatusFlag, active.StatusFlag);
        Assert.True(store.TryGetPoisonState(monster, expiresAt, out _));
        Assert.False(store.TryGetPoisonState(monster, expiresAt.AddTicks(1), out _));
    }

    [Fact]
    public void ApplyPoisonRefreshesExistingMonsterPoisonState()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var first = store.ApplyPoison(monster, poisonLevel: 1, TimeSpan.FromSeconds(5), now);
        var second = store.ApplyPoison(monster, poisonLevel: 7, TimeSpan.FromSeconds(20), now.AddSeconds(2));

        Assert.True(first.Applied);
        Assert.True(second.Applied);
        Assert.True(store.TryGetPoisonState(monster, now.AddSeconds(21), out var active));
        Assert.Equal(7, active.PoisonLevel);
        Assert.Equal(now.AddSeconds(22), active.ExpiresAt);
        Assert.False(store.TryGetPoisonState(monster, now.AddSeconds(22).AddTicks(1), out _));
    }

    [Fact]
    public void ApplyPoisonRejectsBossMonsters()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Monster(hitpoints: 100, restSeconds: 2, monsterId: 4171, boss: true);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        var result = store.ApplyPoison(boss, poisonLevel: 3, TimeSpan.FromSeconds(5), now);

        Assert.False(result.Applied);
        Assert.False(result.AlreadyDead);
        Assert.True(result.BossImmune);
        Assert.False(result.Poison.HasValue);
        Assert.False(store.TryGetPoisonState(boss, now, out _));
    }

    [Fact]
    public void PoisonUsesRemoveOnDeadSemanticsAndDoesNotSurviveRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        Assert.True(store.ApplyPoison(monster, poisonLevel: 3, TimeSpan.FromSeconds(30), now).Applied);
        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);

        Assert.False(store.TryGetPoisonState(monster, now.AddSeconds(2), out _));

        var whileDead = store.ApplyPoison(monster, poisonLevel: 4, TimeSpan.FromSeconds(30), now.AddSeconds(2));
        Assert.False(whileDead.Applied);
        Assert.True(whileDead.AlreadyDead);
        Assert.False(whileDead.BossImmune);

        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out var respawned));
        Assert.Equal(100U, respawned.Hitpoints);
        Assert.False(store.TryGetPoisonState(monster, now.AddSeconds(11), out _));

        Assert.True(store.ApplyPoison(monster, poisonLevel: 5, TimeSpan.FromSeconds(30), now.AddSeconds(11)).Applied);
        Assert.True(store.TryGetPoisonState(monster, now.AddSeconds(11), out var afterRespawn));
        Assert.Equal(5, afterRespawn.PoisonLevel);
    }

    [Theory]
    [InlineData(0x16)]
    [InlineData(0x07)]
    [InlineData(0x08)]
    [InlineData(0x09)]
    public void ApplyTimedStatusFlagTracksTimerVectorAndStrictExpiryBoundary(int statusFlag)
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-07-13T00:00:00Z");
        var expiresAt = now.AddSeconds(45);

        var result = store.ApplyTimedStatusFlag(
            monster,
            statusFlag,
            TimeSpan.FromSeconds(45),
            now);

        Assert.True(result.Applied);
        Assert.False(result.AlreadyDead);
        Assert.False(result.AlreadyActive);
        Assert.True(result.Status.HasValue);
        Assert.Equal(statusFlag, result.Status.Value.StatusFlag);
        Assert.Equal(expiresAt, result.Status.Value.ExpiresAt);
        Assert.True(result.Status.Value.RemoveOnDead);
        Assert.True(store.TryGetTimedStatusFlag(monster, statusFlag, expiresAt, out _));

        var activeVector = store.CreateStatusFlagVector(
            monster,
            expiresAt,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, activeVector.Length);
        Assert.True(IsTimedStatusFlagSet(activeVector, statusFlag));
        Assert.False(store.TryExpireTimedStatusFlags(
            monster,
            expiresAt,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
            out _));

        Assert.True(store.TryExpireTimedStatusFlags(
            monster,
            expiresAt.AddTicks(1),
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
            out var expiredVector));
        Assert.False(IsTimedStatusFlagSet(expiredVector, statusFlag));
        Assert.False(store.TryGetTimedStatusFlag(
            monster,
            statusFlag,
            expiresAt.AddTicks(1),
            out _));
        Assert.False(store.TryExpireTimedStatusFlags(
            monster,
            expiresAt.AddTicks(2),
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
            out _));
    }

    [Fact]
    public void ApplyTimedStatusFlagWithoutRefreshPreservesActiveMagicShieldTimer()
    {
        const int magicShieldStatusFlag = 0x08;
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-07-13T00:00:00Z");

        var first = store.ApplyTimedStatusFlag(
            monster,
            magicShieldStatusFlag,
            TimeSpan.FromSeconds(45),
            now,
            refreshIfActive: false);
        var second = store.ApplyTimedStatusFlag(
            monster,
            magicShieldStatusFlag,
            TimeSpan.FromMinutes(2),
            now.AddSeconds(1),
            refreshIfActive: false);

        Assert.True(first.Applied);
        Assert.True(first.Status.HasValue);
        Assert.False(second.Applied);
        Assert.False(second.AlreadyDead);
        Assert.True(second.AlreadyActive);
        Assert.Equal(first.Status, second.Status);
        Assert.True(store.TryGetTimedStatusFlag(
            monster,
            magicShieldStatusFlag,
            now.AddSeconds(44),
            out var active));
        Assert.Equal(now.AddSeconds(45), active.ExpiresAt);
    }

    [Fact]
    public void TimedStatusFlagsClearOnDeathAndDoNotSurviveRespawn()
    {
        const int invisibilityStatusFlag = 0x16;
        const int stigmaStatusFlag = 0x09;
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-07-13T00:00:00Z");

        Assert.True(store.ApplyTimedStatusFlag(
            monster,
            invisibilityStatusFlag,
            TimeSpan.FromMinutes(1),
            now).Applied);
        Assert.True(store.ApplyTimedStatusFlag(
            monster,
            stigmaStatusFlag,
            TimeSpan.FromMinutes(1),
            now).Applied);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);
        Assert.False(store.TryGetTimedStatusFlag(
            monster,
            invisibilityStatusFlag,
            now.AddSeconds(2),
            out _));
        Assert.False(store.TryGetTimedStatusFlag(
            monster,
            stigmaStatusFlag,
            now.AddSeconds(2),
            out _));
        Assert.All(
            store.CreateStatusFlagVector(
                monster,
                now.AddSeconds(2),
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength),
            value => Assert.Equal(0UL, value));

        var whileDead = store.ApplyTimedStatusFlag(
            monster,
            invisibilityStatusFlag,
            TimeSpan.FromMinutes(1),
            now.AddSeconds(2));
        Assert.False(whileDead.Applied);
        Assert.True(whileDead.AlreadyDead);
        Assert.False(whileDead.AlreadyActive);
        Assert.False(whileDead.Status.HasValue);

        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out _));
        Assert.False(store.TryGetTimedStatusFlag(
            monster,
            invisibilityStatusFlag,
            now.AddSeconds(11),
            out _));
        Assert.False(store.TryGetTimedStatusFlag(
            monster,
            stigmaStatusFlag,
            now.AddSeconds(11),
            out _));
        Assert.All(
            store.CreateStatusFlagVector(
                monster,
                now.AddSeconds(11),
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength),
            value => Assert.Equal(0UL, value));
    }

    [Fact]
    public void NextLegacyGeneratedItemQualityUsesLegacyPerFamilyRateWatchers()
    {
        var store = new MonsterRuntimeStateStore();

        var qualities = Enumerable
            .Range(0, 10)
            .Select(_ => store.NextLegacyGeneratedItemQuality(monsterFamilyId: 18))
            .ToArray();

        Assert.Equal([3, 3, 3, 3, 3, 3, 3, 3, 3, 6], qualities);
        Assert.Equal(3, store.NextLegacyGeneratedItemQuality(monsterFamilyId: 19));
    }

    [Fact]
    public void TryMoveTowardTargetUsesFallbackDirectionWhenTargetFacingCandidateIsBlocked()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var rejectedTargetFacingCandidate = false;

        Assert.True(store.TryMoveTowardTarget(
            monster,
            targetX: 103,
            targetY: 100,
            now,
            TimeSpan.Zero,
            out var moved,
            out var direction,
            isCandidateTileValid: (x, y) =>
            {
                rejectedTargetFacingCandidate |= x == 101 && y == 100;
                return x != 101 || y != 100;
            },
            nextFallbackDirection: static () => 0));

        Assert.True(rejectedTargetFacingCandidate);
        Assert.Equal(0U, direction);
        Assert.Equal(100, moved.X);
        Assert.Equal(101, moved.Y);
    }

    [Fact]
    public void TryMoveTowardTargetRejectsBlockedDirectAndFallbackWithoutMutatingOrThrottling()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var moveInterval = TimeSpan.FromSeconds(5);
        var rejectedTargetFacingCandidate = false;
        var rejectedFallbackCandidate = false;

        Assert.False(store.TryMoveTowardTarget(
            monster,
            targetX: 103,
            targetY: 100,
            now,
            moveInterval,
            out _,
            out var blockedDirection,
            isCandidateTileValid: (x, y) =>
            {
                if (x == 101 && y == 100)
                {
                    rejectedTargetFacingCandidate = true;
                    return false;
                }

                if (x == 100 && y == 101)
                {
                    rejectedFallbackCandidate = true;
                    return false;
                }

                return true;
            },
            nextFallbackDirection: static () => 0));
        Assert.True(rejectedTargetFacingCandidate);
        Assert.True(rejectedFallbackCandidate);
        Assert.Equal(0U, blockedDirection);

        Assert.True(store.TryGetVisibleInstance(monster, now, out var blockedVisible));
        Assert.Equal(100, blockedVisible.X);
        Assert.Equal(100, blockedVisible.Y);

        Assert.True(store.TryMoveTowardTarget(
            monster,
            targetX: 103,
            targetY: 100,
            now,
            moveInterval,
            out var moved,
            out var direction,
            isCandidateTileValid: static (_, _) => true));
        Assert.Equal(6U, direction);
        Assert.Equal(101, moved.X);
        Assert.Equal(100, moved.Y);
    }

    [Fact]
    public void TryMoveTowardTargetTracksPositionAndResetsOnRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        Assert.True(store.TryMoveTowardTarget(
            monster,
            targetX: 103,
            targetY: 100,
            now,
            TimeSpan.Zero,
            out var moved,
            out var direction));
        Assert.Equal(6U, direction);
        Assert.Equal(101, moved.X);
        Assert.Equal(100, moved.Y);

        Assert.True(store.TryGetVisibleInstance(monster, now, out var visible));
        Assert.Equal(101, visible.X);
        Assert.Equal(100, visible.Y);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(1));
        Assert.True(kill.Killed);
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(11), out var respawned));
        Assert.Equal(monster.X, respawned.X);
        Assert.Equal(monster.Y, respawned.Y);
    }

    [Fact]
    public void TryMoveToForcesVisiblePositionAndRejectsDeadOrBlockedTargets()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Monster(hitpoints: 100, restSeconds: 2, monsterId: 2700);
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");

        Assert.True(store.TryMoveTo(monster, 102, 100, now, out var moved));
        Assert.Equal(102, moved.X);
        Assert.Equal(100, moved.Y);

        Assert.True(store.TryGetVisibleInstance(monster, now, out var visible));
        Assert.Equal(102, visible.X);
        Assert.Equal(100, visible.Y);

        Assert.False(store.TryMoveTo(
            monster,
            103,
            100,
            now.AddSeconds(1),
            out _,
            isCandidateTileValid: static (_, _) => false));
        Assert.True(store.TryGetVisibleInstance(monster, now.AddSeconds(1), out visible));
        Assert.Equal(102, visible.X);
        Assert.Equal(100, visible.Y);

        var kill = store.ApplyDamage(monster, requestedDamage: 100, now.AddSeconds(2));
        Assert.True(kill.Killed);
        Assert.False(store.TryMoveTo(monster, 104, 100, now.AddSeconds(3), out _));
    }

    private static LegacyMonsterInstance Monster(
        uint hitpoints,
        int restSeconds,
        uint monsterId = 76,
        bool boss = false)
    {
        return new LegacyMonsterInstance(
            Uid: 470_000,
            MapId: 1000,
            X: 100,
            Y: 100,
            Mesh: 200,
            Level: 83,
            Hitpoints: hitpoints,
            MaxLife: hitpoints,
            MonsterId: monsterId,
            Name: "BladeGhostL83",
            Boss: boss,
            GeneratorId: 70,
            SpawnIndex: 0,
            RestSeconds: restSeconds);
    }

    private static LegacyMonsterTargetSnapshot Target(uint uid, int distance)
    {
        return new LegacyMonsterTargetSnapshot(
            uid,
            Distance: distance,
            Alive: true,
            Flying: false,
            SocketAlive: true);
    }

    private static bool IsTimedStatusFlagSet(IReadOnlyList<ulong> values, int statusFlag)
    {
        var wordIndex = statusFlag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (statusFlag % 32))) != 0;
    }
}
