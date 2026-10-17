using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class SobNpcRuntimeStateStoreTests
{
    [Fact]
    public void OwnerGuildIdSurvivesHitpointsMeshAndNameChanges()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            name: "AttackPole",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.PoleKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        store.SetOwnerGuildId(sobNpc, ownerGuildId: 88);

        store.ApplyDamage(sobNpc, requestedDamage: 150, now, deathMeshOverride: 1_551);
        store.ResetHitpointsToMax(sobNpc, visibleNameOverride: "CapturedGuild");
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(88U, store.GetOwnerGuildId(sobNpc));
        Assert.Equal("CapturedGuild", visible.Name);
        Assert.Equal(100, visible.Life);
        Assert.Equal(100, visible.MaxLife);
        Assert.Equal(1_551, visible.Lookface);
    }

    [Fact]
    public void TryMarkGuildWarGateDestroyedReturnsTrueOnlyOnceUntilReset()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: LegacySobNpcEventRules.GuildWarLeftGateUid,
            name: "LeftGate",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.GateKind,
            lookface: 1_008);

        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));
        Assert.False(store.TryMarkGuildWarGateDestroyed(sobNpc));

        store.ResetHitpointsToMax(sobNpc);

        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));
    }

    [Fact]
    public void ResetHitpointsToMaxClearsGuildWarGateDestroyedFlagWithoutClearingOwner()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: LegacySobNpcEventRules.GuildWarRightGateUid,
            name: "RightGate",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.GateKind,
            lookface: 1_008);

        store.SetOwnerGuildId(sobNpc, ownerGuildId: 88);
        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));

        store.ResetHitpointsToMax(sobNpc, visibleNameOverride: "CapturedGuild");

        Assert.Equal(88U, store.GetOwnerGuildId(sobNpc));
        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));
    }

    [Fact]
    public void ResetGuildWarGateRestoresSeedViewClearsDestroyedFlagAndStoresOwner()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: LegacySobNpcEventRules.GuildWarLeftGateUid,
            name: "LeftGate",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.GateKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");

        store.ApplyDamage(
            sobNpc,
            requestedDamage: 150,
            now,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));
        Assert.Equal(
            LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh,
            store.GetVisible(sobNpc).Lookface);

        store.ResetGuildWarGate(sobNpc, ownerGuildId: 1_363);
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(100, visible.Life);
        Assert.Equal(100, visible.MaxLife);
        Assert.Equal(1_008, visible.Lookface);
        Assert.Equal("LeftGate", visible.Name);
        Assert.Equal(1_363U, store.GetOwnerGuildId(sobNpc));
        Assert.True(store.TryMarkGuildWarGateDestroyed(sobNpc));
    }

    [Fact]
    public void ResetGuildWarGateCanApplyConstructHitpointsAndLevelMeshes()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: LegacySobNpcEventRules.GuildWarRightGateUid,
            name: "RightGate",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.GateKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");

        store.ResetGuildWarGate(
            sobNpc,
            ownerGuildId: 1_363,
            maxHitpoints: 160_000_000,
            closeMeshOverride: LegacySobNpcEventRules.GuildWarRightGateClose4Mesh,
            demolitionMeshOverride: LegacySobNpcEventRules.GuildWarRightGateDemolition4Mesh);
        var visibleAfterReset = store.GetVisible(sobNpc);

        var death = store.ApplyDamage(
            sobNpc,
            requestedDamage: 200_000_000,
            now,
            LegacySobNpcEventRules.GetDeathBehavior(sobNpc));
        var visibleAfterDeath = store.GetVisible(sobNpc);

        Assert.Equal(160_000_000, visibleAfterReset.Life);
        Assert.Equal(160_000_000, visibleAfterReset.MaxLife);
        Assert.Equal(LegacySobNpcEventRules.GuildWarRightGateClose4Mesh, visibleAfterReset.Lookface);
        Assert.True(death.Killed);
        Assert.Equal(160_000_000U, death.DamageApplied);
        Assert.Equal(160_000_000U, death.RemainingHitpoints);
        Assert.Equal(160_000_000, visibleAfterDeath.Life);
        Assert.Equal(160_000_000, visibleAfterDeath.MaxLife);
        Assert.Equal(LegacySobNpcEventRules.GuildWarRightGateDemolition4Mesh, visibleAfterDeath.Lookface);
    }

    [Fact]
    public void ApplyDamageTracksRuntimeHitpointsForNormalSobNpc()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 100, maxLife: 120);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        var result = store.ApplyDamage(sobNpc, requestedDamage: 40, now);
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(40U, result.DamageApplied);
        Assert.Equal(60U, result.RemainingHitpoints);
        Assert.False(result.Killed);
        Assert.False(result.AlreadyDead);
        Assert.Equal(60, visible.Life);
        Assert.Equal(120, visible.MaxLife);
    }

    [Fact]
    public void ApplyDamageIgnoresAlreadyDeadNonTrainingSobNpc()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 25, maxLife: 25, mapId: 1002, kind: 21, lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        var kill = store.ApplyDamage(sobNpc, requestedDamage: 50, now);
        var whileDead = store.ApplyDamage(sobNpc, requestedDamage: 10, now.AddSeconds(1));
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(25U, kill.DamageApplied);
        Assert.Equal(0U, kill.RemainingHitpoints);
        Assert.True(kill.Killed);
        Assert.False(kill.AlreadyDead);
        Assert.Equal(0U, whileDead.DamageApplied);
        Assert.Equal(0U, whileDead.RemainingHitpoints);
        Assert.False(whileDead.Killed);
        Assert.True(whileDead.AlreadyDead);
        Assert.Equal(0, visible.Life);
        Assert.Equal(1_008, visible.Lookface);
    }

    [Fact]
    public void ApplyDamageCanSetDeathMeshForKilledNonTrainingSobNpc()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 25, maxLife: 25, mapId: 1002, kind: 21, lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        var kill = store.ApplyDamage(
            sobNpc,
            requestedDamage: 50,
            now,
            deathMeshOverride: 1_551);
        var whileDead = store.ApplyDamage(sobNpc, requestedDamage: 10, now.AddSeconds(1));
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(25U, kill.DamageApplied);
        Assert.Equal(0U, kill.RemainingHitpoints);
        Assert.True(kill.Killed);
        Assert.False(kill.AlreadyDead);
        Assert.Equal(0U, whileDead.DamageApplied);
        Assert.Equal(0U, whileDead.RemainingHitpoints);
        Assert.False(whileDead.Killed);
        Assert.True(whileDead.AlreadyDead);
        Assert.Equal(0, visible.Life);
        Assert.Equal(25, visible.MaxLife);
        Assert.Equal(1_551, visible.Lookface);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh)]
    public void ApplyDamageWithGateDemolitionResetsGuildWarGateDeath(int id, int expectedMesh)
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: id,
            name: "Gate",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.GateKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");
        var behavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc);

        var lethal = store.ApplyDamage(sobNpc, requestedDamage: 150, now, behavior);
        var visibleAfterLethal = store.GetVisible(sobNpc);
        var nextHit = store.ApplyDamage(sobNpc, requestedDamage: 10, now.AddSeconds(1), behavior);
        var visibleAfterNextHit = store.GetVisible(sobNpc);

        Assert.Equal(LegacySobNpcDeathBehavior.GateDemolition, behavior);
        Assert.Equal(100U, lethal.DamageApplied);
        Assert.Equal(100U, lethal.RemainingHitpoints);
        Assert.True(lethal.Killed);
        Assert.False(lethal.AlreadyDead);
        Assert.Equal(100, visibleAfterLethal.Life);
        Assert.Equal(100, visibleAfterLethal.MaxLife);
        Assert.Equal(expectedMesh, visibleAfterLethal.Lookface);
        Assert.Equal(10U, nextHit.DamageApplied);
        Assert.Equal(90U, nextHit.RemainingHitpoints);
        Assert.False(nextHit.Killed);
        Assert.False(nextHit.AlreadyDead);
        Assert.Equal(90, visibleAfterNextHit.Life);
        Assert.Equal(expectedMesh, visibleAfterNextHit.Lookface);
    }

    [Fact]
    public void ApplyDamageWithPersistentDeathKeepsGenericEventPoleDead()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            id: 25_005,
            name: "AttackPole",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.PoleKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");
        var behavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc);

        var lethal = store.ApplyDamage(sobNpc, requestedDamage: 150, now, behavior);
        var whileDead = store.ApplyDamage(sobNpc, requestedDamage: 10, now.AddSeconds(1), behavior);
        var visible = store.GetVisible(sobNpc);

        Assert.Equal(LegacySobNpcDeathBehavior.PersistentDeath, behavior);
        Assert.Equal(100U, lethal.DamageApplied);
        Assert.Equal(0U, lethal.RemainingHitpoints);
        Assert.True(lethal.Killed);
        Assert.False(lethal.AlreadyDead);
        Assert.Equal(0U, whileDead.DamageApplied);
        Assert.Equal(0U, whileDead.RemainingHitpoints);
        Assert.False(whileDead.Killed);
        Assert.True(whileDead.AlreadyDead);
        Assert.Equal(0, visible.Life);
        Assert.Equal(100, visible.MaxLife);
        Assert.Equal(1_008, visible.Lookface);
    }

    [Fact]
    public void ResetHitpointsToMaxAppliesVisibleNameOverride()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(name: "AttackPole", life: 100, maxLife: 120);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        store.ApplyDamage(sobNpc, requestedDamage: 45, now);

        store.ResetHitpointsToMax(sobNpc, visibleNameOverride: "CapturedGuild");
        var visible = store.GetVisible(sobNpc);

        Assert.Equal("CapturedGuild", visible.Name);
        Assert.Equal(120, visible.Life);
        Assert.Equal(120, visible.MaxLife);
    }

    [Fact]
    public void OwnerGuildIdDefaultsToZeroAndCanBeUpdated()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 100, maxLife: 120);

        Assert.Equal(0U, store.GetOwnerGuildId(sobNpc));
        Assert.False(store.TryGetOwnerGuildId(sobNpc, out var defaultOwner));
        Assert.Equal(0U, defaultOwner);

        store.SetOwnerGuildId(sobNpc, ownerGuildId: 88);

        Assert.Equal(88U, store.GetOwnerGuildId(sobNpc));
        Assert.True(store.TryGetOwnerGuildId(sobNpc, out var owner));
        Assert.Equal(88U, owner);
    }

    [Fact]
    public void ResetHitpointsToMaxCanPersistOwnerGuildIdWithVisibleName()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(name: "AttackPole", life: 100, maxLife: 120);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        store.ApplyDamage(sobNpc, requestedDamage: 45, now);

        store.ResetHitpointsToMax(
            sobNpc,
            visibleNameOverride: "CapturedGuild",
            ownerGuildId: 77);
        var visible = store.GetVisible(sobNpc);

        Assert.Equal("CapturedGuild", visible.Name);
        Assert.Equal(120, visible.Life);
        Assert.Equal(120, visible.MaxLife);
        Assert.Equal(77U, store.GetOwnerGuildId(sobNpc));
    }

    [Fact]
    public void ResetHitpointsToMaxPreservesExistingMeshOverride()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(
            name: "AttackPole",
            life: 100,
            maxLife: 100,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            kind: LegacySobNpcEventRules.PoleKind,
            lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        store.ApplyDamage(sobNpc, requestedDamage: 150, now, deathMeshOverride: 1_551);

        store.ResetHitpointsToMax(sobNpc, visibleNameOverride: "CapturedGuild");
        var visible = store.GetVisible(sobNpc);
        var nextHit = store.ApplyDamage(sobNpc, requestedDamage: 10, now.AddSeconds(1));

        Assert.Equal("CapturedGuild", visible.Name);
        Assert.Equal(100, visible.Life);
        Assert.Equal(100, visible.MaxLife);
        Assert.Equal(1_551, visible.Lookface);
        Assert.Equal(10U, nextHit.DamageApplied);
        Assert.Equal(90U, nextHit.RemainingHitpoints);
        Assert.False(nextHit.AlreadyDead);
    }

    [Fact]
    public void ResetHitpointsToMaxWithoutNameOverrideKeepsSeedName()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(name: "AttackPole", life: 100, maxLife: 120);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        store.ApplyDamage(sobNpc, requestedDamage: 35, now);

        store.ResetHitpointsToMax(sobNpc);
        var visible = store.GetVisible(sobNpc);

        Assert.Equal("AttackPole", visible.Name);
        Assert.Equal(120, visible.Life);
        Assert.Equal(120, visible.MaxLife);
        Assert.Equal(sobNpc.Lookface, visible.Lookface);
    }

    [Fact]
    public void ApplyDeathMeshOnlyChangesVisibleMeshAfterDeath()
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 25, maxLife: 25, mapId: 1002, kind: 21, lookface: 1_008);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        var beforeDeath = store.ApplyDeathMesh(sobNpc, meshOverride: 1_551);
        var visibleBeforeDeath = store.GetVisible(sobNpc);
        var kill = store.ApplyDamage(sobNpc, requestedDamage: 25, now);
        var afterDeath = store.ApplyDeathMesh(sobNpc, meshOverride: 1_551);
        var visibleAfterDeath = store.GetVisible(sobNpc);

        Assert.False(beforeDeath);
        Assert.Equal(1_008, visibleBeforeDeath.Lookface);
        Assert.True(kill.Killed);
        Assert.True(afterDeath);
        Assert.Equal(0, visibleAfterDeath.Life);
        Assert.Equal(1_551, visibleAfterDeath.Lookface);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(22)]
    public void ApplyDamageResetsMap1039TrainingObjectsOnLethalDamage(int kind)
    {
        var store = new SobNpcRuntimeStateStore();
        var sobNpc = SobNpc(life: 0, maxLife: 0, mapId: 1039, kind);
        var now = DateTimeOffset.Parse("2026-06-11T00:00:00Z");

        var lethal = store.ApplyDamage(sobNpc, requestedDamage: 12_000, now);
        var visibleAfterLethal = store.GetVisible(sobNpc);
        var nextHit = store.ApplyDamage(sobNpc, requestedDamage: 5, now.AddSeconds(1));

        Assert.Equal(10_000U, lethal.DamageApplied);
        Assert.Equal(10_000U, lethal.RemainingHitpoints);
        Assert.True(lethal.Killed);
        Assert.False(lethal.AlreadyDead);
        Assert.Equal(10_000, visibleAfterLethal.Life);
        Assert.Equal(10_000, visibleAfterLethal.MaxLife);
        Assert.Equal(5U, nextHit.DamageApplied);
        Assert.Equal(9_995U, nextHit.RemainingHitpoints);
        Assert.False(nextHit.Killed);
        Assert.False(nextHit.AlreadyDead);
    }

    private static SobNpc SobNpc(
        int life,
        int maxLife,
        int mapId = 1000,
        int kind = 2,
        int lookface = 0,
        int sourceLine = 1,
        int id = 25_005,
        string name = "Gate")
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: kind,
            Lookface: lookface,
            MapId: mapId,
            X: 85,
            Y: 70,
            Life: life,
            MaxLife: maxLife,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: sourceLine,
            RawLine: $"{id},{name},{kind},{lookface},{mapId},85,70,{life},{maxLife},1,0",
            Fields:
            [
                id.ToString(),
                name,
                kind.ToString(),
                lookface.ToString(),
                mapId.ToString(),
                "85",
                "70",
                life.ToString(),
                maxLife.ToString(),
                "1",
                "0",
            ]);
    }
}
