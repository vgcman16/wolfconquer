using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterRemoveOnDeadLifecycleTests
{
    private static readonly DateTimeOffset DeathAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RemoveOnDeadDefaultsFalseAndSurvivesRuntimePositionCopies()
    {
        var monster = CreateMonster();
        Assert.False(monster.RemoveOnDead);

        var removable = monster with { RemoveOnDead = true };
        var moved = removable with { X = 102, Y = 103, Hitpoints = 75 };
        Assert.True(moved.RemoveOnDead);
        Assert.False(monster.RemoveOnDead);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(35U)]
    public void RemovableMonsterRemainsPresentUntilDamageIsLethal(uint requestedDamage)
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(removeOnDead: true));

        var result = store.ApplyDamage(monster, requestedDamage, DeathAt);

        Assert.False(result.Killed);
        Assert.False(result.AlreadyDead);
        Assert.Equal(requestedDamage, result.DamageApplied);
        Assert.Equal(monster.MaxLife - requestedDamage, result.RemainingHitpoints);
        Assert.True(store.TryGetDynamicMonsterByUid(monster.MapId, monster.DynamicId, monster.Uid, out _, out _));
        var mapRole = store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, [monster]);
        Assert.NotNull(mapRole);
        Assert.True(mapRole.RemoveOnDead);
        Assert.Equal(result.RemainingHitpoints, mapRole.Hitpoints);
        Assert.True(store.TryGetVisibleInstance(monster, DeathAt, out var visible));
        Assert.Equal(result.RemainingHitpoints, visible.Hitpoints);
        Assert.Single(store.GetVisibleDynamicMonsters(monster.MapId, monster.DynamicId, DeathAt));
        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
    }

    [Fact]
    public void RemovedMonsterImmediatelyRejectsRawLookupsButReservesSpawnIdentity()
    {
        // MonsterRole.Dead:4258-4261 leaves the map immediately; normal RemoveView is different.
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(removeOnDead: true));
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);

        AssertRetiredAndUntargetable(store, monster, DeathAt);
        AssertReservedSpawnIdentity(store, monster);
        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
    }

    [Fact]
    public void RemovedMapRoleCannotBeRecoveredThroughStaticInstanceFallback()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = CreateMonster(removeOnDead: true);
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);

        // No dynamic registration exists, so this exercises the actual static fallback branch.
        Assert.Null(store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, [monster]));
        Assert.True(store.ApplyDamage(monster, 1, DeathAt.AddDays(1)).AlreadyDead);
        Assert.False(store.TryConsumeRespawnedInstance(monster, DeathAt.AddDays(1), out _));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RetiredLifecycleEntryWaitsForEveryPendingCleanup(
        bool blackSpotted,
        bool consumeBlackSpotFirst)
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(removeOnDead: true));
        Assert.True(store.TryMoveTo(monster, 102, 103, DeathAt.AddSeconds(-1), out _));
        if (blackSpotted)
        {
            Assert.True(store.TryMarkBlackSpotUntil(monster, DeathAt.AddSeconds(-1), DeathAt.AddMinutes(1)));
        }
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);
        AssertRetiredAndUntargetable(store, monster, DeathAt);
        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(7), out _));

        if (consumeBlackSpotFirst)
        {
            AssertBlackSpotCleanup(store, monster, DeathAt.AddSeconds(1));
            Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        }

        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out var removed));
        Assert.Equal(monster.Uid, removed.Uid);
        Assert.Equal((102, 103), ((int)removed.X, (int)removed.Y));
        Assert.Equal(0U, removed.Hitpoints);
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));

        if (blackSpotted && !consumeBlackSpotFirst)
        {
            Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
            AssertBlackSpotCleanup(store, monster, DeathAt.AddSeconds(8));
        }
        else
        {
            Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, DeathAt.AddSeconds(8), out _));
        }

        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        AssertReservedSpawnIdentity(store, monster);
        AssertRetiredAndUntargetable(store, monster, DeathAt.AddDays(1));
    }

    [Fact]
    public void BlackSpotInactiveAlreadyQueuedBeforeDeathStillKeepsCleanupReachable()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(removeOnDead: true));
        Assert.True(store.TryMarkBlackSpotUntil(monster, DeathAt.AddSeconds(-10), DeathAt.AddSeconds(-1)));
        Assert.False(store.IsBlackSpotted(monster, DeathAt));
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));

        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        Assert.True(store.TryConsumeBlackSpotInactiveInstance(monster, DeathAt.AddSeconds(8), out var inactive));
        Assert.Equal(monster.Uid, inactive.Uid);
        Assert.Equal(0U, inactive.Hitpoints);
        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, DeathAt.AddSeconds(9), out _));
        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        AssertReservedSpawnIdentity(store, monster);
    }

    [Fact]
    public void StaleDamageHealAndRespawnReadsCannotReviveRetiredMonster()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(removeOnDead: true));
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);

        AssertStaleReadsRemainDead(store, monster, DeathAt.AddDays(1));
        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddDays(1), out _));
        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        AssertStaleReadsRemainDead(store, monster, DeathAt.AddYears(1));
        AssertReservedSpawnIdentity(store, monster);
        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
    }

    [Fact]
    public void RetiredUidStaysReservedUntilExplicitInstanceCleanup()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster(uid: 900_000, removeOnDead: true));
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));
        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        AssertReservedSpawnIdentity(store, monster);
        Assert.Equal(900_001U, store.NextLegacyDynamicMonsterUid());

        Assert.Equal(0, store.RemoveDynamicMonsters(monster.MapId, monster.DynamicId + 1));
        AssertReservedSpawnIdentity(store, monster);
        Assert.Equal(1, store.RemoveDynamicMonsters(monster.MapId, monster.DynamicId));
        Assert.False(store.TryGetDynamicMonsterBySpawnIdentity(monster.MapId, monster.DynamicId,
            monster.GeneratorId, monster.SpawnIndex, out _, out _));
        Assert.Null(store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, []));
    }

    [Fact]
    public void DefaultFalseCorpseRemainsRawMapRoleAndRespawnsNormally()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = Register(store, CreateMonster());
        Assert.False(monster.RemoveOnDead);
        Assert.True(store.TryMoveTo(monster, 102, 103, DeathAt.AddSeconds(-1), out _));
        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);
        Assert.True(store.TryGetDynamicMonsterByUid(monster.MapId, monster.DynamicId, monster.Uid, out _, out _));
        var beforeRemoval = store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, []);
        Assert.NotNull(beforeRemoval);
        Assert.Equal((102, 103), ((int)beforeRemoval.X, (int)beforeRemoval.Y));
        Assert.Equal(0U, beforeRemoval.Hitpoints);

        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));
        Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId));
        var afterRemoval = store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, []);
        Assert.NotNull(afterRemoval);
        Assert.Equal((monster.X, monster.Y), (afterRemoval.X, afterRemoval.Y));
        Assert.Equal(0U, afterRemoval.Hitpoints);
        Assert.True(store.TryConsumeRespawnedInstance(monster, DeathAt.AddSeconds(10), out var respawned));
        Assert.Equal(monster.MaxLife, respawned.Hitpoints);
        Assert.True(store.TryGetVisibleInstance(monster, DeathAt.AddSeconds(10), out _));
        Assert.False(store.ApplyDamage(monster, 1, DeathAt.AddSeconds(10)).AlreadyDead);
    }

    [Fact]
    public void RetiringOneMonsterDoesNotHideSameFamilyLivingSibling()
    {
        var store = new MonsterRuntimeStateStore();
        var retired = Register(store, CreateMonster(removeOnDead: true));
        var living = Register(store, CreateMonster(uid: 900_001, removeOnDead: true, spawnIndex: 1));
        Assert.True(store.ApplyDamage(retired, retired.MaxLife, DeathAt).Killed);
        Assert.True(store.TryConsumeDeathRemovalInstance(retired, DeathAt.AddSeconds(8), out _));

        var lifecycle = Assert.Single(store.GetDynamicMonsterLifecycleInstances(living.MapId, living.DynamicId));
        Assert.Equal(living.Uid, lifecycle.Uid);
        var visible = Assert.Single(store.GetVisibleDynamicMonsters(living.MapId, living.DynamicId, DeathAt.AddDays(1)));
        Assert.Equal(living.Uid, visible.Instance.Uid);
        Assert.True(store.TryGetDynamicMonsterByUid(living.MapId, living.DynamicId, living.Uid, out _, out _));
        Assert.NotNull(store.FindMapRoleMonsterByUid(living.MapId, living.Uid, []));
        Assert.False(store.ApplyDamage(living, 1, DeathAt.AddDays(1)).AlreadyDead);
        AssertRetiredAndUntargetable(store, retired, DeathAt.AddDays(1));
    }

    [Fact]
    public void RetirementAndCleanupPreserveNonlethalBossHarmUntilRewardConsumption()
    {
        var store = new MonsterRuntimeStateStore();
        var boss = Register(store, CreateMonster(removeOnDead: true, boss: true, life: 1_000));
        Assert.False(store.ApplyDamage(boss, 100, DeathAt.AddSeconds(-3), 1_001, "First").Killed);
        Assert.False(store.ApplyDamage(boss, 200, DeathAt.AddSeconds(-2), 1_002, "Second").Killed);
        Assert.False(store.ApplyDamage(boss, 150, DeathAt.AddSeconds(-1), 1_001, "FirstUpdated").Killed);
        Assert.True(store.ApplyDamage(boss, 1_000, DeathAt, 1_003, "LastHit").Killed);

        AssertBossHarm(store.GetBossHarmRanking(boss, DeathAt));
        Assert.True(store.TryConsumeDeathRemovalInstance(boss, DeathAt.AddSeconds(8), out _));
        Assert.Empty(store.GetDynamicMonsterLifecycleInstances(boss.MapId, boss.DynamicId));
        AssertStaleReadsRemainDead(store, boss, DeathAt.AddDays(1));
        AssertBossHarm(store.GetBossHarmRanking(boss, DeathAt.AddDays(1)));
        AssertBossHarm(store.ConsumeBossHarmRanking(boss, DeathAt.AddDays(1)));
        Assert.Empty(store.GetBossHarmRanking(boss, DeathAt.AddDays(1)));
        AssertReservedSpawnIdentity(store, boss);
    }

    private static void AssertRetiredAndUntargetable(
        MonsterRuntimeStateStore store,
        LegacyMonsterInstance monster,
        DateTimeOffset now)
    {
        Assert.False(store.TryGetDynamicMonsterByUid(monster.MapId, monster.DynamicId, monster.Uid, out _, out _));
        Assert.Null(store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, []));
        Assert.Null(store.FindMapRoleMonsterByUid(monster.MapId, monster.Uid, [monster]));
        Assert.False(store.TryGetVisibleInstance(monster, now, out _));
        Assert.DoesNotContain(store.GetVisibleDynamicMonsters(monster.MapId, monster.DynamicId, now),
            entry => entry.Instance.Uid == monster.Uid);
    }

    private static void AssertReservedSpawnIdentity(MonsterRuntimeStateStore store, LegacyMonsterInstance monster)
    {
        Assert.True(store.TryGetDynamicMonsterBySpawnIdentity(monster.MapId, monster.DynamicId,
            monster.GeneratorId, monster.SpawnIndex, out var reserved, out var family));
        Assert.Equal(monster.Uid, reserved.Uid);
        Assert.True(reserved.RemoveOnDead);
        Assert.Equal((int)monster.MonsterId, family.Id);
    }

    private static void AssertStaleReadsRemainDead(
        MonsterRuntimeStateStore store,
        LegacyMonsterInstance monster,
        DateTimeOffset now)
    {
        var damage = store.ApplyDamage(monster, 1, now);
        Assert.True(damage.AlreadyDead);
        Assert.False(damage.Killed);
        Assert.Equal(0U, damage.DamageApplied);
        Assert.Equal(0U, damage.RemainingHitpoints);
        var heal = store.ApplyHeal(monster, monster.MaxLife, now);
        Assert.True(heal.AlreadyDead);
        Assert.Equal(0U, heal.HealApplied);
        Assert.Equal(0U, heal.RemainingHitpoints);
        Assert.False(store.TryConsumeRespawnedInstance(monster, now, out _));
        AssertRetiredAndUntargetable(store, monster, now);
    }

    private static void AssertBlackSpotCleanup(
        MonsterRuntimeStateStore store,
        LegacyMonsterInstance monster,
        DateTimeOffset now)
    {
        Assert.True(store.TryConsumeBlackSpotInactiveInstance(monster, now, out var inactive));
        Assert.Equal(monster.Uid, inactive.Uid);
        Assert.Equal((102, 103), ((int)inactive.X, (int)inactive.Y));
        Assert.Equal(0U, inactive.Hitpoints);
        Assert.False(store.TryConsumeBlackSpotInactiveInstance(monster, now, out _));
    }

    private static void AssertBossHarm(IReadOnlyList<LegacyBossHarmRankingEntry> ranking)
    {
        Assert.Collection(ranking,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal(1_001U, first.PlayerUid);
                Assert.Equal("FirstUpdated", first.PlayerName);
                Assert.Equal(250UL, first.Damage);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal(1_002U, second.PlayerUid);
                Assert.Equal("Second", second.PlayerName);
                Assert.Equal(200UL, second.Damage);
            });
        Assert.DoesNotContain(ranking, entry => entry.PlayerUid == 1_003U);
    }

    private static LegacyMonsterInstance Register(MonsterRuntimeStateStore store, LegacyMonsterInstance monster)
    {
        var family = new Monster((int)monster.MonsterId, monster.Name,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = monster.MaxLife.ToString(CultureInfo.InvariantCulture),
                ["lookface"] = monster.Mesh.ToString(CultureInfo.InvariantCulture),
                ["level"] = monster.Level.ToString(CultureInfo.InvariantCulture),
            }, []);
        return store.RegisterDynamicMonster(monster, family);
    }

    private static LegacyMonsterInstance CreateMonster(
        uint uid = 900_000,
        bool removeOnDead = false,
        bool boss = false,
        uint life = 100,
        int spawnIndex = 0)
    {
        var monster = new LegacyMonsterInstance(
            Uid: uid, MapId: 1_000, X: 100, Y: 100, Mesh: 200, Level: 83,
            Hitpoints: life, MaxLife: life, MonsterId: 2_700, Name: "RemoveOnDeadLifecycle",
            Boss: boss, GeneratorId: 70, SpawnIndex: spawnIndex, RestSeconds: 2, DynamicId: 77);
        return removeOnDead ? monster with { RemoveOnDead = true } : monster;
    }
}
