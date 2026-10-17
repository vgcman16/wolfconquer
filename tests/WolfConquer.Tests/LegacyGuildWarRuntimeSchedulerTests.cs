using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildWarRuntimeSchedulerTests
{
    private static readonly TimeSpan ServerOffset = TimeSpan.FromHours(-5);
    private static readonly TimeZoneInfo ServerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "LegacyGuildWarServerTime",
        ServerOffset,
        "Legacy GuildWar Server Time",
        "Legacy GuildWar Server Time");
    private static readonly DateTimeOffset LegacyFridayStart =
        new(2026, 6, 12, 21, 0, 5, ServerOffset);

    [Fact]
    public void TickBeforeLegacyFridayStartKeepsGuildWarInactive()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var scheduler = new LegacyGuildWarRuntimeScheduler(store);

        var transition = scheduler.Tick(LegacyFridayStart.AddSeconds(-1), ServerTimeZone);

        Assert.Equal(LegacyGuildWarRuntimeTransition.None, transition);
        Assert.False(store.IsActive);
        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void TickAtLegacyFridayStartActivatesGuildWar()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var scheduler = new LegacyGuildWarRuntimeScheduler(store);

        var transition = scheduler.Tick(LegacyFridayStart, ServerTimeZone);

        Assert.Equal(LegacyGuildWarRuntimeTransition.Started, transition);
        Assert.True(store.IsActive);
        Assert.True(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.True(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void TickDuringLegacyActiveWindowActivatesGuildWarWhenNoEarlierTickRan()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var scheduler = new LegacyGuildWarRuntimeScheduler(store);

        var transition = scheduler.Tick(LegacyFridayStart.AddMinutes(15), ServerTimeZone);

        Assert.Equal(LegacyGuildWarRuntimeTransition.Started, transition);
        Assert.True(store.IsActive);
        Assert.True(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.True(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void TickAfterConfiguredCountdownEndsDeactivatesGuildWar()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var scheduler = new LegacyGuildWarRuntimeScheduler(store);

        Assert.Equal(
            LegacyGuildWarRuntimeTransition.Started,
            scheduler.Tick(LegacyFridayStart, ServerTimeZone));
        var transition = scheduler.Tick(
            LegacyFridayStart.Add(LegacyGuildWarRuntimeScheduler.ActiveDuration).AddSeconds(1),
            ServerTimeZone);

        Assert.Equal(LegacyGuildWarRuntimeTransition.Stopped, transition);
        Assert.False(store.IsActive);
        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void RepeatedTicksWhileActiveDoNotExtendTheOriginalCountdown()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var scheduler = new LegacyGuildWarRuntimeScheduler(store);

        Assert.Equal(
            LegacyGuildWarRuntimeTransition.Started,
            scheduler.Tick(LegacyFridayStart, ServerTimeZone));
        Assert.Equal(
            LegacyGuildWarRuntimeTransition.None,
            scheduler.Tick(LegacyFridayStart.AddMinutes(15), ServerTimeZone));
        Assert.Equal(
            LegacyGuildWarRuntimeTransition.None,
            scheduler.Tick(
                LegacyFridayStart.Add(LegacyGuildWarRuntimeScheduler.ActiveDuration).AddSeconds(-1),
                ServerTimeZone));

        Assert.True(store.IsActive);

        var transition = scheduler.Tick(
            LegacyFridayStart.Add(LegacyGuildWarRuntimeScheduler.ActiveDuration).AddSeconds(1),
            ServerTimeZone);

        Assert.Equal(LegacyGuildWarRuntimeTransition.Stopped, transition);
        Assert.False(store.IsActive);
    }
}
