using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MonsterRuntimeLifecycleParityTests
{
    private static readonly DateTimeOffset DeathAt =
        DateTimeOffset.Parse("2026-07-14T12:00:00Z");

    [Fact]
    public void StaticMonsterRemainsUntargetableBetweenRemovalAndConsumedRespawn()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = CreateMonster(uid: 470_001);

        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);

        Assert.False(store.TryGetVisibleInstance(monster, DeathAt.AddSeconds(5), out _));
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(7), out _));
        Assert.True(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(8), out _));

        var respawnAt = DeathAt.AddSeconds(10);
        Assert.False(store.TryGetVisibleInstance(monster, respawnAt, out _));
        Assert.True(store.ApplyDamage(monster, 1, respawnAt).AlreadyDead);

        Assert.True(store.TryConsumeRespawnedInstance(monster, respawnAt, out var respawned));
        Assert.Equal(monster.X, respawned.X);
        Assert.Equal(monster.Y, respawned.Y);
        Assert.Equal(monster.MaxLife, respawned.Hitpoints);
        Assert.False(store.TryConsumeRespawnedInstance(monster, respawnAt, out _));
        Assert.True(store.TryGetVisibleInstance(monster, respawnAt, out var visible));
        Assert.Equal(monster.MaxLife, visible.Hitpoints);

        var targetable = store.ApplyDamage(monster, 1, respawnAt);
        Assert.False(targetable.AlreadyDead);
        Assert.Equal(1U, targetable.DamageApplied);
    }

    [Fact]
    public void DynamicMonsterCannotResurrectFromVisibilityOrDamageReads()
    {
        var store = new MonsterRuntimeStateStore();
        var monster = CreateMonster(uid: 900_001, dynamicId: 77);
        store.RegisterDynamicMonster(monster, CreateMonsterFamily());

        Assert.True(store.ApplyDamage(monster, monster.MaxLife, DeathAt).Killed);
        Assert.Equal(
            monster,
            Assert.Single(store.GetDynamicMonsterLifecycleInstances(monster.MapId, monster.DynamicId)));

        var respawnAt = DeathAt.AddSeconds(10);
        Assert.False(store.TryConsumeDeathRemovalInstance(monster, DeathAt.AddSeconds(7), out _));
        Assert.False(store.TryConsumeRespawnedInstance(monster, respawnAt, out _));
        Assert.Empty(store.GetVisibleDynamicMonsters(monster.MapId, monster.DynamicId, respawnAt));
        Assert.False(store.TryGetVisibleInstance(monster, respawnAt, out _));
        Assert.True(store.ApplyDamage(monster, 1, respawnAt).AlreadyDead);

        Assert.True(store.TryConsumeDeathRemovalInstance(monster, respawnAt, out _));
        Assert.False(store.TryGetVisibleInstance(monster, respawnAt, out _));
        Assert.True(store.ApplyDamage(monster, 1, respawnAt).AlreadyDead);

        Assert.True(store.TryConsumeRespawnedInstance(monster, respawnAt, out var respawned));
        Assert.Equal(monster.MaxLife, respawned.Hitpoints);
        Assert.Equal(
            monster.Uid,
            Assert.Single(store.GetVisibleDynamicMonsters(monster.MapId, monster.DynamicId, respawnAt))
                .Instance
                .Uid);

        var targetable = store.ApplyDamage(monster, 1, respawnAt);
        Assert.False(targetable.AlreadyDead);
        Assert.Equal(1U, targetable.DamageApplied);
    }

    [Fact]
    public void DynamicRespawnSuppressionKeepsDeadTargetsAbsentWithoutHidingLivingTargets()
    {
        var store = new MonsterRuntimeStateStore();
        var family = CreateMonsterFamily();
        var deadMonster = CreateMonster(uid: 900_010, dynamicId: 88, spawnIndex: 0);
        var livingMonster = CreateMonster(uid: 900_011, dynamicId: 88, spawnIndex: 1);
        store.RegisterDynamicMonster(deadMonster, family);
        store.RegisterDynamicMonster(livingMonster, family);

        Assert.True(store.ApplyDamage(deadMonster, deadMonster.MaxLife, DeathAt).Killed);
        Assert.Equal(2, store.SuppressDynamicMonsterRespawns(deadMonster.MapId, deadMonster.DynamicId));

        var livingTarget = store.ApplyDamage(livingMonster, 1, DeathAt.AddSeconds(1));
        Assert.False(livingTarget.AlreadyDead);
        Assert.Equal(1U, livingTarget.DamageApplied);
        Assert.Equal(
            livingMonster.Uid,
            Assert.Single(store.GetVisibleDynamicMonsters(
                    deadMonster.MapId,
                    deadMonster.DynamicId,
                    DeathAt.AddSeconds(1)))
                .Instance
                .Uid);

        Assert.True(store.TryConsumeDeathRemovalInstance(deadMonster, DeathAt.AddSeconds(8), out _));
        Assert.False(store.TryConsumeRespawnedInstance(deadMonster, DeathAt.AddDays(1), out _));
        Assert.False(store.TryGetVisibleInstance(deadMonster, DeathAt.AddDays(1), out _));
        Assert.True(store.ApplyDamage(deadMonster, 1, DeathAt.AddDays(1)).AlreadyDead);

        Assert.True(store.TryGetVisibleInstance(livingMonster, DeathAt.AddDays(1), out var visibleLiving));
        Assert.Equal(livingMonster.MaxLife - 1, visibleLiving.Hitpoints);
    }

    private static LegacyMonsterInstance CreateMonster(
        uint uid,
        uint dynamicId = 0,
        int spawnIndex = 0)
    {
        return new LegacyMonsterInstance(
            Uid: uid,
            MapId: 1_000,
            X: 100,
            Y: 100,
            Mesh: 200,
            Level: 83,
            Hitpoints: 100,
            MaxLife: 100,
            MonsterId: 2_700,
            Name: "LifecycleMonster",
            Boss: false,
            GeneratorId: 70,
            SpawnIndex: spawnIndex,
            RestSeconds: 2,
            DynamicId: dynamicId);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            2_700,
            "LifecycleMonster",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "100",
                ["lookface"] = "200",
                ["level"] = "83",
            },
            []);
    }
}
