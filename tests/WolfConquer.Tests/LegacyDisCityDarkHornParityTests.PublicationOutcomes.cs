using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDisCityDarkHornParityTests
{
    [Theory]
    [InlineData(DicCityMap4NpcId)]
    [InlineData(TwinDisCityMainNpcId)]
    public async Task DarkHornObserverFailureKeepsPublishedRewardFileWithoutSuccessOrBroadcast(uint npcId)
    {
        using var harness = CreateHarness(npcId, CreateCapacityInventory(32), CreateCompleteWorldData(), includeObserver: true);
        harness.FailObserver = true;

        Assert.True(await harness.InvokeAsync());

        var published = Assert.IsType<WorldSession>(harness.PublishedAtFailure);
        Assert.False(harness.HasCurrentSession); Assert.Null(published.ActiveNpcId);
        Assert.DoesNotContain(published.PlayerItems.InventoryItems, item => item.ItemId == DarkHornItemId);
        Assert.Equal(9, published.PlayerItems.InventoryItems.Count(item => RewardItemIds.Contains(item.ItemId)));
        AssertPersistedEquals(harness.PlayerItemsRoot, published.PlayerItems);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent);
        Assert.Contains("publicationStage=Observer", harness.Log.ToString(), StringComparison.Ordinal);
        Assert.Contains("compensationSuppressed=true", harness.Log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("rollback=complete", harness.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));
        AssertPersistedEquals(harness.PlayerItemsRoot, published.PlayerItems);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent);
    }

    [Fact]
    public async Task DarkHornAuthorizationChangeBeforeActionPreservesActualSavedItems()
    {
        var items = CreateCapacityInventory(32);
        using var harness = CreateHarness(DicCityMap4NpcId, items, CreateCompleteWorldData(), includeObserver: true);
        var replacement = harness.OriginalSession with { ActiveNpcId = 999 };
        harness.SetBeforeAction(replacement);

        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(replacement, harness.CurrentSession); AssertPersistedEquals(harness.PlayerItemsRoot, items);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent);
    }
}
