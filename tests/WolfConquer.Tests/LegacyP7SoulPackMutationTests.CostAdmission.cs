using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed partial class LegacyP7SoulPackMutationTests
{
    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, 820_073U)]
    [InlineData(WeaponNpcId, WeaponPackItemId, 800_020U)]
    public async Task CostAdmissionSkipsZeroUnboundStackAndPaysOnlyThePositiveRow(uint npcId, uint packId, uint rewardId)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var zero = Item(10, packId, stackSize: 0);
        var paid = Item(20, packId, stackSize: 2);
        var unrelated = Item(30, 710_001);
        unrelated = unrelated.WithMaximumDurabilityAndPlus(unrelated.MaximumDurability, 3);
        unrelated.OwnerName = "Retained";
        unrelated.Signature = "No cost";
        var original = Items(zero, paid, unrelated);
        var result = await InvokeAsync(npcId, 1, original, Catalog(ItemType(rewardId)), itemsRoot);

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;
        AssertPlayerItemsEqual(Items(zero), Items(Assert.Single(rows, row => row.Uid == 10)));
        AssertPlayerItemsEqual(Items(paid.WithStackSize(1)), Items(Assert.Single(rows, row => row.Uid == 20)));
        AssertPlayerItemsEqual(Items(unrelated), Items(Assert.Single(rows, row => row.Uid == 30)));
        Assert.Equal(2, result.Sent.Count);
        AssertItemUpdate(result.Sent[0], 20, packId, stackSize: 1, bound: 0);
        var reward = Assert.Single(rows, row => row.ItemId == rewardId);
        AssertSoulGrant(result.Sent[1], reward.Uid, rewardId, durability: 1);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, 820_073U, (ushort)1)]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, 820_073U, (ushort)2)]
    [InlineData(WeaponNpcId, WeaponPackItemId, 800_020U, (ushort)1)]
    [InlineData(WeaponNpcId, WeaponPackItemId, 800_020U, (ushort)2)]
    public async Task CostAdmissionRetainsOriginalEligibleOrder(uint npcId, uint packId, uint rewardId, ushort firstStack)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var first = Item(20, packId, firstStack);
        var second = Item(10, packId);
        second.Signature = "Second eligible";
        var result = await InvokeAsync(npcId, 1, Items(first, second), Catalog(ItemType(rewardId)), itemsRoot);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(Items(second), Items(Assert.Single(rows, row => row.Uid == 10)));
        if (firstStack == 1)
        {
            Assert.DoesNotContain(rows, row => row.Uid == 20);
            AssertItemRemoval(result.Sent[0], 20);
        }
        else
        {
            AssertPlayerItemsEqual(Items(first.WithStackSize(1)), Items(Assert.Single(rows, row => row.Uid == 20)));
            AssertItemUpdate(result.Sent[0], 20, packId, stackSize: 1, bound: 0);
        }
        Assert.Equal(2, result.Sent.Count);
        AssertSoulGrant(result.Sent[1], Assert.Single(rows, row => row.ItemId == rewardId).Uid, rewardId, durability: 1);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, 820_073U)]
    [InlineData(WeaponNpcId, WeaponPackItemId, 800_020U)]
    public async Task ForeignSingletonCannotFalselyFreeSoulRewardSlot(uint npcId, uint packId, uint rewardId)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var rows = new List<PlayerItemSnapshotItem> { Item(10, packId, bound: 1), Item(20, packId, stackSize: 2) };
        rows.AddRange(Enumerable.Range(100, 38).Select(uid => Item((uint)uid, (uint)(410_000 + uid))));
        var original = Items(rows.ToArray());
        var preimage = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(npcId, 1, original, Catalog(ItemType(rewardId)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(preimage, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-pack-removal", result.Log);
        Assert.Equal(preimage, File.ReadAllBytes(Path.Combine(itemsRoot, CharacterUid + ".bin")));
        AssertPersistedEquals(itemsRoot, original);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, 820_073U)]
    [InlineData(WeaponNpcId, WeaponPackItemId, 800_020U)]
    public async Task ZeroUnboundAndPositiveForeignSoulCostsCannotGrant(uint npcId, uint packId, uint rewardId)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var original = Items(Item(10, packId, stackSize: 0), Item(20, packId, bound: 1));
        var result = await InvokeAsync(npcId, 1, original, Catalog(ItemType(rewardId)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        AssertMessageBox(result.Sent, MissingPackText);
        AssertPersistedEquals(itemsRoot, original);
    }
}
