using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task EarthForceTransactionObserverRetirementIsCommittedWithSuppressedStaleOutput()
    {
        var scene = await RunEarthForceTransactionAsync(4, ownership: "after-commit");
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(scene.ReplacementBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Single(scene.Committed.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745);
        Assert.DoesNotContain(scene.Committed.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.Empty(scene.Sent.Where(IsEarthForceTransactionRewardFrame));
        Assert.Empty(scene.PublicationOwners);
        Assert.DoesNotContain("session-changed-before-commit", scene.Log);
    }

    [Fact]
    public async Task EarthForceTransactionNotificationFailureDoesNotRollBackOrReportPreparationRejection()
    {
        var scene = await RunEarthForceTransactionAsync(4, failPublication: true);
        var failure = Assert.IsType<IOException>(scene.Failure);
        Assert.Equal("owned EarthForce output failure", failure.Message);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Committed, scene.Current);
        Assert.Single(scene.Current!.PlayerItems.InventoryItems, item => item.ItemId == 3_008_745);
        Assert.DoesNotContain(scene.Current.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.Same(scene.Committed, Assert.Single(scene.PublicationOwners));
        Assert.Single(scene.Sent.Where(IsEarthForceTransactionRewardFrame));
        Assert.DoesNotContain("no fragment debit, grant or reward output was committed", scene.Log);
    }
}
