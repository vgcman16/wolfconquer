using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceNewGrantPreservesOneUnboundItemAndFullPacketGolden(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind);
        AssertNatureForceCommitted(scene);
        var item = Assert.Single(scene.Current.PlayerItems.InventoryItems, row => row.ItemId == scene.Spec.Item);
        Assert.Equal(10U, item.Uid);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((ushort)17, item.Durability);
        Assert.Equal((ushort)17, item.MaximumDurability);
        var frame = Assert.Single(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.Equal(EarthForceTransactionItemGolden(10, scene.Spec.Item, 17, 1, 1), frame);
    }

    [Theory]
    [InlineData("rune")]
    [InlineData("star")]
    [InlineData("stone")]
    public async Task NatureForceStackGrantPreservesThePhysicalUidAndExactUpdateFrame(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, existing: 1);
        AssertNatureForceCommitted(scene);
        var item = Assert.Single(scene.Current.PlayerItems.InventoryItems, row => row.ItemId == scene.Spec.Item);
        Assert.Equal(1U, item.Uid);
        Assert.Equal((ushort)2, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal(EarthForceTransactionItemGolden(1, scene.Spec.Item, 0, 3, 2),
            Assert.Single(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item)));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task NatureForceRuneNativeEightLimitNeverGrantsANinthFragment(ushort existing)
    {
        var scene = await RunNatureForceRewardAsync("rune", existing);
        if (existing == 7)
        {
            AssertNatureForceCommitted(scene);
            Assert.Equal(EarthForceTransactionItemGolden(1, scene.Spec.Item, 0, 3, 8),
                Assert.Single(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item)));
        }
        else AssertNatureForceUnchanged(scene);
        Assert.Equal(existing == 7 ? (ushort)8 : existing,
            Assert.Single(scene.Current.PlayerItems.InventoryItems, row => row.ItemId == scene.Spec.Item).StackSize);
        Assert.Contains(scene.Spec.AutoRoute, scene.Log);
    }

    [Theory]
    [InlineData("hammer", 1)]
    [InlineData("wheel", 1)]
    [InlineData("star", 100)]
    [InlineData("stone", 50)]
    public async Task NatureForceAlreadyCollectedBranchPreservesNoGrantAndAutoRouteTiming(string kind, ushort existing)
    {
        var scene = await RunNatureForceRewardAsync(kind, existing, fault: "missing");
        AssertNatureForceUnchanged(scene);
        Assert.Contains(scene.Spec.AutoRoute, scene.Log);
        Assert.DoesNotContain("itemtype.txt", scene.Log);
    }

    [Theory]
    [InlineData("star", 99, 100)]
    [InlineData("stone", 49, 50)]
    public async Task NatureForceStoneAndOreThresholdIsCheckedBeforeTheGrant(string kind, ushort before, ushort after)
    {
        var scene = await RunNatureForceRewardAsync(kind, before);
        AssertNatureForceCommitted(scene);
        Assert.Equal(after, Assert.Single(scene.Current.PlayerItems.InventoryItems, row => row.ItemId == scene.Spec.Item).StackSize);
        Assert.Equal(EarthForceTransactionItemGolden(1, scene.Spec.Item, 0, 3, after),
            Assert.Single(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item)));
        Assert.DoesNotContain(scene.Spec.AutoRoute, scene.Log);
    }

    [Theory]
    [InlineData("rune", 45)]
    [InlineData("wheel", 40)]
    public async Task NatureForceRealRateMissDoesNotConsultGrantMetadata(string kind, int rate)
    {
        var scene = await RunNatureForceRewardAsync(kind, fault: "durability", rate: false);
        AssertNatureForceUnchanged(scene);
        Assert.Contains($"Core.Rate({rate})=false", scene.Log);
        Assert.DoesNotContain("nature-force-metadata-invalid", scene.Log);
    }

    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceFullBagRetainsTruthfulCapacityAndNoReward(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, full: true);
        AssertNatureForceUnchanged(scene);
        Assert.Contains(ParseNpcReplies(scene.Sent), reply => reply.Text == "Please make 1 more space in your inventory.");
        Assert.Contains("reason=inventory-full", scene.Log);
    }

    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceInactiveQuestDoesNotInventCapacityOrGrantResponse(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, accepted: false, full: true);
        AssertNatureForceUnchanged(scene);
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), reply => reply.Text == "Please make 1 more space in your inventory.");
    }

    [Theory]
    [InlineData("rune", false)]
    [InlineData("wheel", true)]
    public async Task NatureForceRateAndFullBagKeepTheirDistinctSourceOrdering(string kind, bool capacity)
    {
        var scene = await RunNatureForceRewardAsync(kind, rate: false, full: true);
        AssertNatureForceUnchanged(scene);
        var messages = ParseNpcReplies(scene.Sent).Where(reply => reply.Text == "Please make 1 more space in your inventory.");
        if (capacity) Assert.Single(messages);
        else Assert.Empty(messages);
    }

    private static void AssertNatureForceCommitted(NatureForceScene scene)
    {
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Committed);
        Assert.Same(scene.Committed, scene.Current);
        AssertNatureForceOriginalAndFiles(scene);
        Assert.Single(scene.PublicationOwners);
        Assert.All(scene.PublicationOwners, owner => Assert.Same(scene.Committed, owner));
        Assert.Same(scene.Original.PlayerItems.InventoryItems[0], scene.Current.PlayerItems.InventoryItems[0]);
    }
}
