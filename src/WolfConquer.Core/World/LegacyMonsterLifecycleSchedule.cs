using System;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

public sealed class LegacyMonsterLifecycleSchedule
{
    public const int FadeAwayDelaySeconds = 5;
    public const int RemoveFromViewAfterFadeSeconds = 3;
    public const int RemoveFromViewDelaySeconds = FadeAwayDelaySeconds + RemoveFromViewAfterFadeSeconds;
    public const int DefaultRespawnTimeMinSeconds = 10;
    public const int DefaultRespawnTimeMaxExclusiveSeconds = 20;
    public const int SaturnRespawnTimeMinSeconds = 100;
    public const int SaturnRespawnTimeMaxExclusiveSeconds = 200;
    public const int FixedHourRespawnTimeSeconds = 3600;

    public static readonly TimeSpan FadeAwayDelay = TimeSpan.FromSeconds(FadeAwayDelaySeconds);
    public static readonly TimeSpan RemoveFromViewAfterFadeDelay = TimeSpan.FromSeconds(RemoveFromViewAfterFadeSeconds);
    public static readonly TimeSpan RemoveFromViewDelay = TimeSpan.FromSeconds(RemoveFromViewDelaySeconds);

    private LegacyMonsterLifecycleSchedule(
        DateTimeOffset deathAt,
        int restSeconds,
        int normalizedRestSeconds,
        DateTimeOffset fadeAwayDueAt,
        DateTimeOffset removeFromViewDueAt,
        DateTimeOffset respawnReadyAt,
        DateTimeOffset? respawnEffectDueAt)
    {
        DeathAt = deathAt;
        RestSeconds = restSeconds;
        NormalizedRestSeconds = normalizedRestSeconds;
        RespawnTimeSeconds = restSeconds;
        NormalizedRespawnTimeSeconds = normalizedRestSeconds;
        FadeAwayDueAt = fadeAwayDueAt;
        RemoveFromViewDueAt = removeFromViewDueAt;
        RespawnReadyAt = respawnReadyAt;
        RespawnEffectDueAt = respawnEffectDueAt;
    }

    public DateTimeOffset DeathAt { get; }

    public int RestSeconds { get; }

    public int NormalizedRestSeconds { get; }

    public int RespawnTimeSeconds { get; }

    public int NormalizedRespawnTimeSeconds { get; }

    public DateTimeOffset FadeAwayDueAt { get; }

    public DateTimeOffset RemoveFromViewDueAt { get; }

    public DateTimeOffset RespawnReadyAt { get; }

    public DateTimeOffset? RespawnEffectDueAt { get; }

    public IReadOnlyList<LegacyMonsterLifecycleEvent> Events
    {
        get
        {
            if (RespawnEffectDueAt is { } respawnEffectDueAt)
            {
                return
                [
                    new(LegacyMonsterLifecycleEventType.FadeAway, FadeAwayDueAt),
                    new(LegacyMonsterLifecycleEventType.RemoveFromView, RemoveFromViewDueAt),
                    new(LegacyMonsterLifecycleEventType.RespawnReady, RespawnReadyAt),
                    new(LegacyMonsterLifecycleEventType.RespawnEffect, respawnEffectDueAt)
                ];
            }

            return
            [
                new(LegacyMonsterLifecycleEventType.FadeAway, FadeAwayDueAt),
                new(LegacyMonsterLifecycleEventType.RemoveFromView, RemoveFromViewDueAt),
                new(LegacyMonsterLifecycleEventType.RespawnReady, RespawnReadyAt)
            ];
        }
    }

    public static LegacyMonsterLifecycleSchedule FromDeath(
        DateTimeOffset deathAt,
        int restSeconds,
        bool includeRespawnEffect = true)
    {
        return Create(deathAt, restSeconds, includeRespawnEffect);
    }

    public static int ResolveRespawnTimeSeconds(LegacyMonsterInstance instance, Random random)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(random);

        var defaultRandomRespawnTimeSeconds = random.Next(
            DefaultRespawnTimeMinSeconds,
            DefaultRespawnTimeMaxExclusiveSeconds);
        var saturnRandomRespawnTimeSeconds = instance.MonsterId == 20_211
            ? random.Next(SaturnRespawnTimeMinSeconds, SaturnRespawnTimeMaxExclusiveSeconds)
            : SaturnRespawnTimeMinSeconds;

        return ResolveRespawnTimeSeconds(
            instance.MonsterId,
            instance.Boss,
            instance.MaxLife,
            instance.RestSeconds,
            defaultRandomRespawnTimeSeconds,
            saturnRandomRespawnTimeSeconds);
    }

    public static int ResolveRespawnTimeSeconds(
        uint monsterId,
        bool boss,
        uint maxLife,
        int restSeconds,
        int defaultRandomRespawnTimeSeconds,
        int saturnRandomRespawnTimeSeconds = SaturnRespawnTimeMinSeconds)
    {
        var respawnTimeSeconds = ValidateRandomRespawnTimeSeconds(
            defaultRandomRespawnTimeSeconds,
            DefaultRespawnTimeMinSeconds,
            DefaultRespawnTimeMaxExclusiveSeconds,
            nameof(defaultRandomRespawnTimeSeconds));

        if (boss && maxLife > 100_000 && maxLife < 7_000_000)
        {
            respawnTimeSeconds = FixedHourRespawnTimeSeconds;
        }

        if (monsterId == 20_211)
        {
            respawnTimeSeconds = ValidateRandomRespawnTimeSeconds(
                saturnRandomRespawnTimeSeconds,
                SaturnRespawnTimeMinSeconds,
                SaturnRespawnTimeMaxExclusiveSeconds,
                nameof(saturnRandomRespawnTimeSeconds));
        }

        if (UsesGeneratorRestSecondsAsRespawnTime(monsterId))
        {
            respawnTimeSeconds = restSeconds;
        }

        if (UsesFixedHourRespawnTime(monsterId))
        {
            respawnTimeSeconds = FixedHourRespawnTimeSeconds;
        }

        return respawnTimeSeconds;
    }

    public static bool UsesGeneratorRestSecondsAsRespawnTime(uint monsterId)
    {
        return monsterId is 2_700 or 2_699 or 7_022 or 2_164 or 2_165;
    }

    public static bool UsesFixedHourRespawnTime(uint monsterId)
    {
        return monsterId is 20_055 or 4_171 or 32_070;
    }

    public static LegacyMonsterLifecycleSchedule Create(
        DateTimeOffset deathAt,
        int restSeconds,
        bool includeRespawnEffect = true)
    {
        var normalizedRestSeconds = Math.Max(0, restSeconds);

        // Legacy MonsterRole uses DeadStamp + 5s for FadeAway, then FadeAway + 3s for view removal.
        var fadeAwayDueAt = deathAt + FadeAwayDelay;
        var removeFromViewDueAt = fadeAwayDueAt + RemoveFromViewAfterFadeDelay;
        var respawnReadyAt = removeFromViewDueAt + TimeSpan.FromSeconds(normalizedRestSeconds);
        var respawnEffectDueAt = includeRespawnEffect ? respawnReadyAt : (DateTimeOffset?)null;

        return new LegacyMonsterLifecycleSchedule(
            deathAt,
            restSeconds,
            normalizedRestSeconds,
            fadeAwayDueAt,
            removeFromViewDueAt,
            respawnReadyAt,
            respawnEffectDueAt);
    }

    private static int ValidateRandomRespawnTimeSeconds(
        int value,
        int minInclusive,
        int maxExclusive,
        string parameterName)
    {
        if (value < minInclusive || value >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Legacy random respawn time must be in [{minInclusive}, {maxExclusive}).");
        }

        return value;
    }

    public LegacyMonsterLifecyclePhase GetPhase(DateTimeOffset now)
    {
        if (now < FadeAwayDueAt)
        {
            return LegacyMonsterLifecyclePhase.Dead;
        }

        if (now < RemoveFromViewDueAt)
        {
            return LegacyMonsterLifecyclePhase.FadeAway;
        }

        if (now < RespawnReadyAt)
        {
            return LegacyMonsterLifecyclePhase.RemovedFromView;
        }

        return LegacyMonsterLifecyclePhase.RespawnReady;
    }

    public IReadOnlyList<LegacyMonsterLifecycleEvent> GetDueEvents(DateTimeOffset now)
    {
        var events = Events;
        var dueEvents = new List<LegacyMonsterLifecycleEvent>(events.Count);
        foreach (var lifecycleEvent in events)
        {
            if (now >= lifecycleEvent.DueAt)
            {
                dueEvents.Add(lifecycleEvent);
            }
        }

        return dueEvents;
    }
}

public enum LegacyMonsterLifecyclePhase
{
    Dead = 0,
    FadeAway = 1,
    RemovedFromView = 2,
    RespawnReady = 3
}

public enum LegacyMonsterLifecycleEventType
{
    FadeAway = 0,
    RemoveFromView = 1,
    RespawnReady = 2,
    RespawnEffect = 3
}

public readonly record struct LegacyMonsterLifecycleEvent(
    LegacyMonsterLifecycleEventType Type,
    DateTimeOffset DueAt);
