using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterTargetLockTests
{
    [Fact]
    public void AdvanceAttackUsesStrictLegacyAttackIntervalBoundary()
    {
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var interval = TimeSpan.FromMilliseconds(500);
        var target = Target(distance: 3);

        var first = LegacyMonsterTargetLock.AdvanceAttack(
            LegacyMonsterTargetLockState.Empty,
            target,
            attackRange: 4,
            boss: false,
            hitpoints: 100,
            now,
            interval);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, first.Action);
        Assert.True(first.AttackDue);
        Assert.Equal(target.Uid, first.State.LastTargetUid);
        Assert.Equal(now, first.State.LastAttackAt);

        var atBoundary = LegacyMonsterTargetLock.AdvanceAttack(
            first.State,
            target,
            attackRange: 4,
            boss: false,
            hitpoints: 100,
            now.AddMilliseconds(500),
            interval);

        Assert.Equal(LegacyMonsterTargetLockAction.WaitForAttackInterval, atBoundary.Action);
        Assert.False(atBoundary.AttackDue);
        Assert.Equal(now, atBoundary.State.LastAttackAt);

        var afterBoundary = LegacyMonsterTargetLock.AdvanceAttack(
            atBoundary.State,
            target,
            attackRange: 4,
            boss: false,
            hitpoints: 100,
            now.AddMilliseconds(501),
            interval);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, afterBoundary.Action);
        Assert.True(afterBoundary.AttackDue);
        Assert.Equal(now.AddMilliseconds(501), afterBoundary.State.LastAttackAt);
    }

    [Theory]
    [InlineData(5, true, false, true)]
    [InlineData(3, false, false, true)]
    [InlineData(3, true, true, true)]
    [InlineData(3, true, false, false)]
    public void AdvanceAttackSendsInvalidTargetsBackToSearchWithoutStamping(
        int distance,
        bool alive,
        bool flying,
        bool socketAlive)
    {
        var lastAttackAt = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var state = new LegacyMonsterTargetLockState(
            LastTargetUid: 1_001,
            LastAttackAt: lastAttackAt);
        var now = lastAttackAt.AddSeconds(5);

        var result = LegacyMonsterTargetLock.AdvanceAttack(
            state,
            Target(distance, alive: alive, flying: flying, socketAlive: socketAlive),
            attackRange: 4,
            boss: false,
            hitpoints: 100,
            now,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, result.Action);
        Assert.False(result.AttackDue);
        Assert.Null(result.State.LastTargetUid);
        Assert.Equal(lastAttackAt, result.State.LastAttackAt);
    }

    [Fact]
    public void DecideNextActionClearsInvalidTargetBeforeSearchingAgain()
    {
        var lastAttackAt = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var state = new LegacyMonsterTargetLockState(
            LastTargetUid: 1_001,
            LastAttackAt: lastAttackAt);
        var now = lastAttackAt.AddSeconds(5);

        var invalid = LegacyMonsterTargetLock.DecideNextAction(
            state,
            Target(uid: 1_001, distance: 3, alive: false),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            boss: false,
            hitpoints: 100,
            now,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, invalid.Action);
        Assert.False(invalid.AttackDue);
        Assert.Null(invalid.State.LastTargetUid);
        Assert.Equal(lastAttackAt, invalid.State.LastAttackAt);

        var replacement = LegacyMonsterTargetLock.DecideNextAction(
            invalid.State,
            Target(uid: 2_002, distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            boss: false,
            hitpoints: 100,
            now,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, replacement.Action);
        Assert.True(replacement.AttackDue);
        Assert.Equal(2_002U, replacement.State.LastTargetUid);
        Assert.Equal(now, replacement.State.LastAttackAt);
    }

    [Fact]
    public void AdvanceAttackUsesLegacyHighHealthBossEighteenTileRange()
    {
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");

        var normal = LegacyMonsterTargetLock.AdvanceAttack(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 18),
            attackRange: 4,
            boss: false,
            hitpoints: 3_000_000,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, normal.Action);
        Assert.Equal(4, normal.EffectiveAttackRange);
        Assert.Equal(4, normal.EffectiveScanRange);

        var boss = LegacyMonsterTargetLock.AdvanceAttack(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 18),
            attackRange: 4,
            boss: true,
            hitpoints: 3_000_000,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, boss.Action);
        Assert.True(boss.AttackDue);
        Assert.Equal(18, boss.EffectiveAttackRange);
        Assert.Equal(18, boss.EffectiveScanRange);
    }

    [Fact]
    public void DecideNextActionRetainsScannedTargetWithoutStampingAttackTime()
    {
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");

        var retained = LegacyMonsterTargetLock.DecideNextAction(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 8),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            boss: false,
            hitpoints: 100,
            now,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.RetainTarget, retained.Action);
        Assert.False(retained.AttackDue);
        Assert.Equal(1_001U, retained.State.LastTargetUid);
        Assert.Null(retained.State.LastAttackAt);
        Assert.Equal(4, retained.EffectiveAttackRange);
        Assert.Equal(18, retained.EffectiveScanRange);

        var attack = LegacyMonsterTargetLock.DecideNextAction(
            retained.State,
            Target(distance: 3),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 18,
            boss: false,
            hitpoints: 100,
            now.AddMilliseconds(100),
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, attack.Action);
        Assert.True(attack.AttackDue);
        Assert.Equal(now.AddMilliseconds(100), attack.State.LastAttackAt);
    }

    [Fact]
    public void DecideNextActionHighHealthBossAcquiresAndAttacksAtEighteenTiles()
    {
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");

        var normal = LegacyMonsterTargetLock.DecideNextAction(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 18),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 4,
            boss: false,
            hitpoints: 3_000_000,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, normal.Action);
        Assert.Equal(4, normal.EffectiveScanRange);

        var lowHitpointBoss = LegacyMonsterTargetLock.DecideNextAction(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 18),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 4,
            boss: true,
            hitpoints: 1_999_999,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, lowHitpointBoss.Action);
        Assert.Equal(4, lowHitpointBoss.EffectiveScanRange);

        var highHitpointBoss = LegacyMonsterTargetLock.DecideNextAction(
            LegacyMonsterTargetLockState.Empty,
            Target(distance: 18),
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 4,
            boss: true,
            hitpoints: 2_000_001,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, highHitpointBoss.Action);
        Assert.True(highHitpointBoss.AttackDue);
        Assert.Equal(18, highHitpointBoss.EffectiveAttackRange);
        Assert.Equal(18, highHitpointBoss.EffectiveScanRange);
    }

    [Fact]
    public void DecideNextActionUsesInclusiveBossRangeOnlyForAcquiredTarget()
    {
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var target = Target(distance: 18);

        var searching = LegacyMonsterTargetLock.DecideNextAction(
            LegacyMonsterTargetLockState.Empty,
            target,
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 4,
            boss: true,
            hitpoints: 2_000_000,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.SearchTarget, searching.Action);
        Assert.False(searching.AttackDue);
        Assert.Equal(4, searching.EffectiveAttackRange);
        Assert.Equal(4, searching.EffectiveScanRange);

        var attacking = LegacyMonsterTargetLock.DecideNextAction(
            new LegacyMonsterTargetLockState(target.Uid, LastAttackAt: null),
            target,
            LegacyMonsterTargetLockLifecycle.Alive,
            attackRange: 4,
            scanRange: 4,
            boss: true,
            hitpoints: 2_000_000,
            now,
            TimeSpan.Zero);

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, attacking.Action);
        Assert.True(attacking.AttackDue);
        Assert.Equal(18, attacking.EffectiveAttackRange);
        Assert.Equal(18, attacking.EffectiveScanRange);
    }

    [Fact]
    public void ResetForRespawnClearsRetainedTargetAndAttackCadence()
    {
        var lastAttackAt = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var state = new LegacyMonsterTargetLockState(
            LastTargetUid: 1_001,
            LastAttackAt: lastAttackAt);

        var reset = LegacyMonsterTargetLock.ResetForRespawn();

        Assert.Equal(LegacyMonsterTargetLockState.Empty, reset);
        Assert.Null(reset.LastTargetUid);
        Assert.Null(reset.LastAttackAt);

        var result = LegacyMonsterTargetLock.AdvanceAttack(
            reset,
            Target(distance: 3),
            attackRange: 4,
            boss: false,
            hitpoints: 100,
            lastAttackAt.AddMilliseconds(1),
            TimeSpan.FromMinutes(10));

        Assert.Equal(LegacyMonsterTargetLockAction.Attack, result.Action);
        Assert.True(result.AttackDue);
    }

    [Theory]
    [InlineData(LegacyMonsterTargetLockLifecycle.Dead)]
    [InlineData(LegacyMonsterTargetLockLifecycle.Respawning)]
    public void DecideNextActionResetsDeadOrRespawningMonsterTargetLock(
        LegacyMonsterTargetLockLifecycle lifecycle)
    {
        var lastAttackAt = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        var state = new LegacyMonsterTargetLockState(
            LastTargetUid: 1_001,
            LastAttackAt: lastAttackAt);

        var result = LegacyMonsterTargetLock.DecideNextAction(
            state,
            Target(distance: 3),
            lifecycle,
            attackRange: 4,
            scanRange: 18,
            boss: false,
            hitpoints: 100,
            lastAttackAt.AddSeconds(1),
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(LegacyMonsterTargetLockAction.ResetTargetLock, result.Action);
        Assert.False(result.AttackDue);
        Assert.Equal(LegacyMonsterTargetLockState.Empty, result.State);
        Assert.Null(result.State.LastTargetUid);
        Assert.Null(result.State.LastAttackAt);
        Assert.Equal(4, result.EffectiveAttackRange);
        Assert.Equal(18, result.EffectiveScanRange);
    }

    private static LegacyMonsterTargetSnapshot Target(
        int distance,
        uint uid = 1_001,
        bool alive = true,
        bool flying = false,
        bool socketAlive = true)
    {
        return new LegacyMonsterTargetSnapshot(
            uid,
            Distance: distance,
            Alive: alive,
            Flying: flying,
            SocketAlive: socketAlive);
    }
}
