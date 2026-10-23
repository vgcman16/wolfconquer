using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceAfterCommitRetirementSuppressesStaleOutputWithoutClaimingRollback(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, ownership: "after-commit");
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(77, scene.Current.Character.Money);
        Assert.Equal(scene.PhysicalBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current.PlayerItems));
        Assert.Single(scene.Committed.PlayerItems.InventoryItems, item => item.ItemId == scene.Spec.Item);
        AssertNatureForceOriginalAndFiles(scene);
        Assert.Empty(scene.PublicationOwners);
        Assert.DoesNotContain(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.DoesNotContain("WorldServer refused legacy map3998 nature-force reward", scene.Log);
    }

    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceSendFailureRemainsCommittedInsteadOfBecomingPreparationRejection(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, failSend: true);
        var failure = Assert.IsType<IOException>(scene.Failure);
        Assert.Equal("owned nature-force output failure", failure.Message);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Committed, scene.Current);
        Assert.Single(scene.Current.PlayerItems.InventoryItems, item => item.ItemId == scene.Spec.Item);
        AssertNatureForceOriginalAndFiles(scene);
        Assert.Same(scene.Committed, Assert.Single(scene.PublicationOwners));
        Assert.Single(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.DoesNotContain("WorldServer refused legacy map3998 nature-force reward", scene.Log);
    }
}
