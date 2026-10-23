using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 7)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 7)]
    public Task RegisteredZeroOnlyChestKeysRefuseWithoutRewardDebitOrSave(bool epic, byte firstBound) =>
        RunAsync(async fixture =>
        {
            var keyId = epic ? 3_303_974U : 3_303_973U;
            fixture.Seed(40, firstStack: 0, secondStack: 0, firstBound,
                firstKeyItemId: keyId, secondKeyItemId: keyId, npcId: epic ? 22_607U : 22_598U);
            var before = fixture.Session;

            if (epic) await fixture.UnlockEpicAsync(); else await fixture.UnlockRareAsync();

            fixture.AssertUnchanged(before);
            Assert.Empty(fixture.Sent);
            Assert.Contains("key missing.", fixture.Log.ToString());
            Assert.DoesNotContain("chest item reward", fixture.Log.ToString());
            Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
        });

    [Theory]
    [InlineData(false, 40, 1, 0)]
    [InlineData(false, 40, 1, 1)]
    [InlineData(false, 40, 1, 7)]
    [InlineData(false, 39, 2, 0)]
    [InlineData(false, 39, 2, 1)]
    [InlineData(false, 39, 2, 7)]
    [InlineData(true, 40, 1, 0)]
    [InlineData(true, 40, 1, 1)]
    [InlineData(true, 40, 1, 7)]
    [InlineData(true, 39, 2, 0)]
    [InlineData(true, 39, 2, 1)]
    [InlineData(true, 39, 2, 7)]
    public Task RegisteredChestSkipsZeroFirstKeyAndPaysActualPositiveStock(
        bool epic, int slots, ushort positiveStack, byte firstBound) => RunAsync(async fixture =>
    {
        var keyId = epic ? 3_303_974U : 3_303_973U;
        fixture.Seed(slots, firstStack: 0, secondStack: positiveStack, firstBound,
            firstKeyItemId: keyId, secondKeyItemId: keyId, npcId: epic ? 22_607U : 22_598U);
        var zero = fixture.Session.PlayerItems.InventoryItems[0];

        if (epic) await fixture.UnlockEpicAsync(); else await fixture.UnlockRareAsync();

        Assert.Same(zero, fixture.Current.PlayerItems.InventoryItems[0]);
        if (epic)
            fixture.AssertEpicOutcome(keyIndex: 1);
        else
        {
            Assert.Equal(2, fixture.Sent.Count);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(fixture.Sent[1]), out var reward));
            Assert.Contains(reward.ItemId, new uint[] { 3_306_362, 3_303_480, 3_003_124 });
            fixture.AssertItemReward(reward.ItemId, keyIndex: 1);
        }
        Assert.DoesNotContain("inventory is full.", fixture.Log.ToString());
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public Task RegisteredFullChestCannotUseZeroRowAsFreedRewardSlot(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 0, secondStack: 2, firstBound);
        var before = fixture.Session;

        await fixture.UnlockRareAsync();

        fixture.AssertUnchanged(before);
        Assert.Equal(LegacyNpcReplyPacket.ForMessageBox(
            "Please make 1 more space in your inventory.").ToFrameBytes(), Assert.Single(fixture.Sent));
        Assert.Contains("inventory is full.", fixture.Log.ToString());
        Assert.DoesNotContain("chest item reward", fixture.Log.ToString());
        Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
    });

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 7)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 7)]
    public Task ActualCpChestSkipsZeroThenDebitsOnePositiveKey(ushort positiveStack, byte firstBound) =>
        RunAsync(async fixture =>
        {
            fixture.Seed(40, firstStack: 0, secondStack: positiveStack, firstBound);
            var zero = fixture.Session.PlayerItems.InventoryItems[0];

            await fixture.UnlockSelectedAsync(rewardItemId: 0, conquerPointsReward: 200_000);

            Assert.Same(zero, fixture.Current.PlayerItems.InventoryItems[0]);
            fixture.AssertPositiveKeyCpReward(keyIndex: 1);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public Task ActualCpChestCannotRewardZeroOnlyKeys(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 0, secondStack: 0, firstBound);
        var before = fixture.Session;

        await fixture.UnlockSelectedAsync(rewardItemId: 0, conquerPointsReward: 200_000);

        fixture.AssertUnchanged(before);
        Assert.Empty(fixture.Sent);
        Assert.Contains("key missing.", fixture.Log.ToString());
        Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
    });
}
