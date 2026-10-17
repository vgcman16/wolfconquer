using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildWarRuntimeStateStoreTests
{
    [Fact]
    public void DefaultStateIsInactive()
    {
        var store = new LegacyGuildWarRuntimeStateStore();

        Assert.False(store.IsActive);
        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void SetActiveTracksActiveAndInactiveTransitions()
    {
        var store = new LegacyGuildWarRuntimeStateStore();

        store.SetActive(true);

        Assert.True(store.IsActive);

        store.SetActive(false);

        Assert.False(store.IsActive);

        store.Start();

        Assert.True(store.IsActive);

        store.Stop();

        Assert.False(store.IsActive);
    }

    [Fact]
    public void MsgNewSynWarBroadcastHelpersRequireActiveStateAndNonzeroSyndicateId()
    {
        var store = new LegacyGuildWarRuntimeStateStore();

        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 0));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 0));

        store.Start();

        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 0));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 0));
        Assert.True(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.True(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));

        store.Stop();

        Assert.False(store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234));
        Assert.False(store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234));
    }

    [Fact]
    public void RepeatedConcurrentCallsKeepStoreUsable()
    {
        var store = new LegacyGuildWarRuntimeStateStore();
        var activeFailures = 0;
        var inactiveFailures = 0;

        Parallel.For(0, 20_000, _ =>
        {
            store.SetActive(true);

            if (!store.IsActive ||
                !store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234) ||
                !store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234))
            {
                Interlocked.Increment(ref activeFailures);
            }
        });

        Parallel.For(0, 20_000, _ =>
        {
            store.SetActive(false);

            if (store.IsActive ||
                store.CanBroadcastMsgNewSynWarOwnershipChanged(syndicateId: 1234) ||
                store.CanBroadcastMsgNewSynWarDestroyed(syndicateId: 1234))
            {
                Interlocked.Increment(ref inactiveFailures);
            }
        });

        Assert.Equal(0, activeFailures);
        Assert.Equal(0, inactiveFailures);
        Assert.False(store.IsActive);
    }
}
