using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterLifecycleScheduleTests
{
    [Fact]
    public void CreateSchedulesFadeAtDeathPlusFiveSeconds()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, restSeconds: 12);

        Assert.Equal(TimeSpan.FromSeconds(5), LegacyMonsterLifecycleSchedule.FadeAwayDelay);
        Assert.Equal(deathAt.AddSeconds(5), schedule.FadeAwayDueAt);
    }

    [Fact]
    public void CreateSchedulesRemoveFromViewThreeSecondsAfterFadeAndEightSecondsAfterDeath()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, restSeconds: 12);

        Assert.Equal(TimeSpan.FromSeconds(3), LegacyMonsterLifecycleSchedule.RemoveFromViewAfterFadeDelay);
        Assert.Equal(TimeSpan.FromSeconds(8), LegacyMonsterLifecycleSchedule.RemoveFromViewDelay);
        Assert.Equal(schedule.FadeAwayDueAt.AddSeconds(3), schedule.RemoveFromViewDueAt);
        Assert.Equal(deathAt.AddSeconds(8), schedule.RemoveFromViewDueAt);
    }

    [Theory]
    [InlineData(17, 17, 25)]
    [InlineData(0, 0, 8)]
    [InlineData(-30, 0, 8)]
    public void CreateSchedulesRespawnReadyAtRemoveFromViewPlusNormalizedRespawnTime(
        int respawnTimeSeconds,
        int expectedNormalizedRespawnTimeSeconds,
        int expectedRespawnReadySecondsAfterDeath)
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, respawnTimeSeconds);

        Assert.Equal(respawnTimeSeconds, schedule.RespawnTimeSeconds);
        Assert.Equal(expectedNormalizedRespawnTimeSeconds, schedule.NormalizedRespawnTimeSeconds);
        Assert.Equal(
            schedule.RemoveFromViewDueAt.AddSeconds(expectedNormalizedRespawnTimeSeconds),
            schedule.RespawnReadyAt);
        Assert.Equal(
            deathAt.AddSeconds(expectedRespawnReadySecondsAfterDeath),
            schedule.RespawnReadyAt);
    }

    [Fact]
    public void ResolveRespawnTimeUsesFamilyRandomForNormalMonsters()
    {
        var respawnTimeSeconds = LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(
            monsterId: 76,
            boss: false,
            maxLife: 100,
            restSeconds: 2,
            defaultRandomRespawnTimeSeconds: 17,
            saturnRandomRespawnTimeSeconds: 150);

        Assert.Equal(17, respawnTimeSeconds);
    }

    [Theory]
    [InlineData(2700U)]
    [InlineData(2699U)]
    [InlineData(7022U)]
    [InlineData(2164U)]
    [InlineData(2165U)]
    public void ResolveRespawnTimeUsesGeneratorRestSecondsForSourceBackedMonsterIds(uint monsterId)
    {
        var respawnTimeSeconds = LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(
            monsterId,
            boss: false,
            maxLife: 100,
            restSeconds: 44,
            defaultRandomRespawnTimeSeconds: 17,
            saturnRandomRespawnTimeSeconds: 150);

        Assert.Equal(44, respawnTimeSeconds);
    }

    [Fact]
    public void ResolveRespawnTimeUsesFixedHourForSourceBackedBossHealthRange()
    {
        var respawnTimeSeconds = LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(
            monsterId: 76,
            boss: true,
            maxLife: 200_000,
            restSeconds: 2,
            defaultRandomRespawnTimeSeconds: 17,
            saturnRandomRespawnTimeSeconds: 150);

        Assert.Equal(3600, respawnTimeSeconds);
    }

    [Fact]
    public void ResolveRespawnTimeUsesSaturnRandomWindowForSaturnMonster()
    {
        var respawnTimeSeconds = LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(
            monsterId: 20_211,
            boss: false,
            maxLife: 100,
            restSeconds: 2,
            defaultRandomRespawnTimeSeconds: 17,
            saturnRandomRespawnTimeSeconds: 150);

        Assert.Equal(150, respawnTimeSeconds);
    }

    [Theory]
    [InlineData(20055U)]
    [InlineData(4171U)]
    [InlineData(32070U)]
    public void ResolveRespawnTimeUsesFixedHourForSourceBackedMonsterIds(uint monsterId)
    {
        var respawnTimeSeconds = LegacyMonsterLifecycleSchedule.ResolveRespawnTimeSeconds(
            monsterId,
            boss: false,
            maxLife: 100,
            restSeconds: 44,
            defaultRandomRespawnTimeSeconds: 17,
            saturnRandomRespawnTimeSeconds: 150);

        Assert.Equal(3600, respawnTimeSeconds);
    }

    [Fact]
    public void GetPhaseSelectsBoundaryPhasesDeterministically()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, restSeconds: 2);

        Assert.Equal(
            LegacyMonsterLifecyclePhase.Dead,
            schedule.GetPhase(schedule.FadeAwayDueAt.AddTicks(-1)));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.FadeAway,
            schedule.GetPhase(schedule.FadeAwayDueAt));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.FadeAway,
            schedule.GetPhase(schedule.FadeAwayDueAt.AddTicks(1)));

        Assert.Equal(
            LegacyMonsterLifecyclePhase.FadeAway,
            schedule.GetPhase(schedule.RemoveFromViewDueAt.AddTicks(-1)));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.RemovedFromView,
            schedule.GetPhase(schedule.RemoveFromViewDueAt));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.RemovedFromView,
            schedule.GetPhase(schedule.RemoveFromViewDueAt.AddTicks(1)));

        Assert.Equal(
            LegacyMonsterLifecyclePhase.RemovedFromView,
            schedule.GetPhase(schedule.RespawnReadyAt.AddTicks(-1)));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.RespawnReady,
            schedule.GetPhase(schedule.RespawnReadyAt));
        Assert.Equal(
            LegacyMonsterLifecyclePhase.RespawnReady,
            schedule.GetPhase(schedule.RespawnReadyAt.AddTicks(1)));
    }

    [Fact]
    public void EventsIncludeSourceBackedRespawnEffectByDefault()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, restSeconds: 4);

        Assert.Equal(
        [
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.FadeAway, deathAt.AddSeconds(5)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RemoveFromView, deathAt.AddSeconds(8)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RespawnReady, deathAt.AddSeconds(12)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RespawnEffect, deathAt.AddSeconds(12))
        ], schedule.Events);
        Assert.Equal(schedule.RespawnReadyAt, schedule.RespawnEffectDueAt);
    }

    [Fact]
    public void EventsCanOmitRespawnEffectWhenLegacyCallerSuppressesIt()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(
            deathAt,
            restSeconds: 4,
            includeRespawnEffect: false);

        Assert.Equal(
        [
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.FadeAway, deathAt.AddSeconds(5)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RemoveFromView, deathAt.AddSeconds(8)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RespawnReady, deathAt.AddSeconds(12))
        ], schedule.Events);
        Assert.Null(schedule.RespawnEffectDueAt);
    }

    [Fact]
    public void GetDueEventsReturnsAllEventsAtOrBeforeNow()
    {
        var deathAt = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var schedule = LegacyMonsterLifecycleSchedule.Create(deathAt, restSeconds: 2);

        Assert.Empty(schedule.GetDueEvents(schedule.FadeAwayDueAt.AddTicks(-1)));
        Assert.Equal(
        [
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.FadeAway, deathAt.AddSeconds(5))
        ], schedule.GetDueEvents(schedule.FadeAwayDueAt));
        Assert.Equal(
        [
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.FadeAway, deathAt.AddSeconds(5)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RemoveFromView, deathAt.AddSeconds(8))
        ], schedule.GetDueEvents(schedule.RemoveFromViewDueAt));
        Assert.Equal(
        [
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.FadeAway, deathAt.AddSeconds(5)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RemoveFromView, deathAt.AddSeconds(8)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RespawnReady, deathAt.AddSeconds(10)),
            new LegacyMonsterLifecycleEvent(LegacyMonsterLifecycleEventType.RespawnEffect, deathAt.AddSeconds(10))
        ], schedule.GetDueEvents(schedule.RespawnReadyAt));
    }
}
