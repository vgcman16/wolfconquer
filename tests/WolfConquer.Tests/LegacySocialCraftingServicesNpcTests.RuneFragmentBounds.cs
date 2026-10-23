using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySocialCraftingServicesNpcTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuneFragmentNpcPublicOwnerConsumesOnlyAdmittedBoundRows(bool oppositeFirst)
    {
        var matching = CreateInventoryItem(2_001, RuneFragmentItemId, stackSize: 20, bound: 1);
        var opposite = CreateInventoryItem(3_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        using var fixture = new RuneFragmentBoundFixture(
            oppositeFirst ? [opposite, matching] : [matching, opposite]);

        await fixture.ApplyAsync();

        var current = fixture.Current.PlayerItems;
        Assert.DoesNotContain(current.InventoryItems, item => item.Uid == matching.Uid);
        Assert.Same(opposite, Assert.Single(current.InventoryItems, item => item.Uid == opposite.Uid));
        var reward = fixture.AssertReward();
        Assert.Equal(3, fixture.Sent.Count);
        Assert.Equal(new MsgItemUsagePacket(matching.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0)
            .ToFrameBytes(), fixture.Sent[0]);
        fixture.AssertRewardFrames(reward, firstRewardFrame: 1);
        fixture.AssertCommitted([opposite, reward]);
    }

    [Fact]
    public async Task RuneFragmentNpcPublicOwnerRetainsPerStackBatchRemovalThenUpdateOrder()
    {
        var opposite = CreateInventoryItem(3_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        var first = CreateInventoryItem(2_001, RuneFragmentItemId, stackSize: 12, bound: 1);
        var second = CreateInventoryItem(2_002, RuneFragmentItemId, stackSize: 13, bound: 1)
            .WithColor(4).WithRuneExperience(77);
        using var fixture = new RuneFragmentBoundFixture([opposite, first, second]);

        await fixture.ApplyAsync();

        var remainder = second.WithStackSize(5);
        RuneFragmentBoundFixture.AssertPhysicalItemEqual(remainder, Assert.Single(fixture.Current.PlayerItems.InventoryItems,
            item => item.Uid == second.Uid));
        Assert.Same(opposite, Assert.Single(fixture.Current.PlayerItems.InventoryItems,
            item => item.Uid == opposite.Uid));
        var reward = fixture.AssertReward();
        Assert.Equal(4, fixture.Sent.Count);
        Assert.Equal(new MsgItemUsagePacket(first.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0)
            .ToFrameBytes(), fixture.Sent[0]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(
            remainder, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), fixture.Sent[1]);
        fixture.AssertRewardFrames(reward, firstRewardFrame: 2);
        fixture.AssertCommitted([opposite, remainder, reward]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuneFragmentNpcPublicOwnerNineteenMatchingDoesNotBorrowOppositeBoundStock(bool oppositeFirst)
    {
        var matching = CreateInventoryItem(2_001, RuneFragmentItemId, stackSize: 19, bound: 1);
        var opposite = CreateInventoryItem(3_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        using var fixture = new RuneFragmentBoundFixture(
            oppositeFirst ? [opposite, matching] : [matching, opposite]);

        await fixture.ApplyAsync();

        fixture.AssertUnchangedRefusal();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuneFragmentNpcPublicOwnerStagedAutoBindOnlyAuthorizesItsMatchingRow(bool oppositeFirst)
    {
        // The native NPC contract binds the selected special-fragment row. The
        // lower UID selects it even when another row precedes it physically.
        var selected = CreateInventoryItem(2_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        var untouched = CreateInventoryItem(3_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        using var fixture = new RuneFragmentBoundFixture(
            oppositeFirst ? [untouched, selected] : [selected, untouched]);

        await fixture.ApplyAsync();

        Assert.Equal((byte)0, selected.Bound);
        Assert.Same(untouched, Assert.Single(fixture.Current.PlayerItems.InventoryItems,
            item => item.Uid == untouched.Uid));
        Assert.Equal((byte)0, untouched.Bound);
        var reward = fixture.AssertReward();
        Assert.Equal(3, fixture.Sent.Count);
        Assert.Equal(new MsgItemUsagePacket(selected.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0)
            .ToFrameBytes(), fixture.Sent[0]);
        fixture.AssertRewardFrames(reward, firstRewardFrame: 1);
        fixture.AssertCommitted([untouched, reward]);
    }

    [Fact]
    public async Task RuneFragmentNpcPublicOwnerRefusalDoesNotPublishStagedAutoBind()
    {
        var selected = CreateInventoryItem(2_001, RuneFragmentItemId, stackSize: 19, bound: 0);
        var other = CreateInventoryItem(3_001, RuneFragmentItemId, stackSize: 20, bound: 0);
        using var fixture = new RuneFragmentBoundFixture([other, selected]);

        await fixture.ApplyAsync();

        fixture.AssertUnchangedRefusal();
        Assert.All(fixture.Current.PlayerItems.InventoryItems, item => Assert.Equal((byte)0, item.Bound));
    }
}
