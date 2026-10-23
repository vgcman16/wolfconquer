using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// CURRENT API packet. The canonical future-UID fixture requires its own real
// Host migration; these tests do not install, fake or bypass that owner.
public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public Task RegisteredRareChestRefusesWhenActualFirstDebitDoesNotFreeSlot(
        ushort secondStack, byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 2, secondStack, firstBound);
        var before = fixture.Session;
        await fixture.UnlockRareAsync();

        fixture.AssertUnchanged(before);
        Assert.Equal(
            LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
            Assert.Single(fixture.Sent));
        Assert.Contains("inventory is full.", fixture.Log.ToString());
        Assert.DoesNotContain("chest item reward", fixture.Log.ToString());
        Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
    });

    [Theory]
    [InlineData(40, 1, 0)]
    [InlineData(40, 1, 1)]
    [InlineData(39, 2, 0)]
    [InlineData(39, 2, 1)]
    public Task RegisteredRareChestChargesFirstRowAndKeepsValidRewardGoldens(
        int slots, ushort firstStack, byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(slots, firstStack, secondStack: firstStack == 1 ? (ushort)2 : (ushort)1, firstBound);
        await fixture.UnlockRareAsync();

        Assert.Equal(2, fixture.Sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(fixture.Sent[1]), out var reward));
        Assert.Contains(reward.ItemId, new uint[] { 3_306_362, 3_303_480, 3_003_124 });
        fixture.AssertItemReward(reward.ItemId);
    });

    [Theory]
    [InlineData(3_306_362U)]
    [InlineData(3_303_480U)]
    [InlineData(3_003_124U)]
    [InlineData(4_200_005U)]
    [InlineData(3_007_269U)]
    [InlineData(3_004_247U)]
    public Task RealUnlockRetainsEachExistingItemPayout(uint rewardItemId) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 1);
        await fixture.UnlockSelectedAsync(rewardItemId, conquerPointsReward: 0);
        fixture.AssertItemReward(rewardItemId);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task RealCpBranchNeedsNoFreeSlotAndPreservesDebitThenScalarOrder(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 2, secondStack: 1, firstBound);
        var before = fixture.Session;
        await fixture.UnlockSelectedAsync(rewardItemId: 0, conquerPointsReward: 200_000);

        var updated = fixture.Current;
        Assert.Equal(40, updated.PlayerItems.InventoryItems.Count);
        fixture.AssertItems(fixture.ExpectedDebit());
        Assert.Equal(before with
        {
            Character = before.Character with { ConquerPoints = 200_055 },
            PlayerItems = updated.PlayerItems,
        }, updated);
        Assert.Equal(2, fixture.Sent.Count);
        fixture.AssertDebitFrame();
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(fixture.Sent[1]), out var update));
        Assert.Equal((uint)before.Character.Uid, update.Uid);
        Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_055) }, update.Updates);
        fixture.AssertCharacterCpSaved();
        Assert.Contains("chest CP reward", fixture.Log.ToString());
        Assert.DoesNotContain("inventory is full.", fixture.Log.ToString());
        Assert.DoesNotContain("PARITY-GAP", fixture.Log.ToString());
    });

    [Fact]
    public Task RegisteredMissingKeyLeavesSessionAndFilesUnchangedWithoutSuccess() => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 2, secondStack: 1, firstBound: 0, includeKeys: false);
        var before = fixture.Session;
        await fixture.UnlockRareAsync();
        fixture.AssertUnchanged(before);
        Assert.Empty(fixture.Sent);
        Assert.Contains("key missing.", fixture.Log.ToString());
    });
}
