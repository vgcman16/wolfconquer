using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneMixedBoundFragmentId = 3_306_366;
    private const uint RuneMixedBoundRewardId = 4_012_001;

    [Fact]
    public async Task HandlerRuneFragmentMixedBoundDebitsOnlyAdmittedBoundTypeAndPreservesOtherStackOnReload()
    {
        const uint unboundUid = 5_337_700;
        const uint boundUid = 5_337_701;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                unboundUid, RuneMixedBoundFragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 20, bound: 0),
            PlayerItemSnapshotLoaderTests.DbItem(
                boundUid, RuneMixedBoundFragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 20, bound: 1));
        var worldData = CreateWorldDataCatalog(
            CreateRuneMixedBoundItemType(RuneMixedBoundFragmentId, "RedRuneFragment", 1, maxStackSize: 20),
            CreateRuneMixedBoundItemType(RuneMixedBoundRewardId, "RedRuneMixedBoundReward", 610, maxStackSize: 1));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var original = GetSession(handler, connection).PlayerItems;
        Assert.Equal(2, original.InventoryItems.Count);
        AssertRuneMixedBoundStack(original, unboundUid, stackSize: 20, bound: 0);
        AssertRuneMixedBoundStack(original, boundUid, stackSize: 20, bound: 1);
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(unboundUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 22, sent.Count);
        var materialFrames = sent.Skip(countBeforeRequest).Take(19).ToArray();
        var updates = materialFrames.Select(ParseRuneMixedBoundItem).ToArray();
        Assert.All(materialFrames, bytes =>
            Assert.False(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _)));
        Assert.All(updates, packet =>
        {
            Assert.Equal(unboundUid, packet.Uid);
            Assert.Equal(RuneMixedBoundFragmentId, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
            Assert.Equal((byte)0, packet.Bound);
        });
        Assert.Equal(Enumerable.Range(1, 19).Reverse().Select(stack => checked((ushort)stack)),
            updates.Select(packet => packet.StackSize));
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 19]), out var removal));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
        Assert.Equal(unboundUid, removal.Id);

        var rewardPacket = ParseRuneMixedBoundItem(sent[countBeforeRequest + 20]);
        Assert.Equal(RuneMixedBoundRewardId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal((byte)0, rewardPacket.Bound);
        Assert.Equal((ushort)1, rewardPacket.StackSize);
        Assert.Equal((ushort)610, rewardPacket.Durability);
        Assert.Equal((ushort)610, rewardPacket.MaximumDurability);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 21]), out var message));
        Assert.Contains("You have got 1 RedRuneMixedBoundReward!.", message.Content);

        var updated = GetSession(handler, connection).PlayerItems;
        Assert.NotSame(original, updated);
        Assert.Equal(2, updated.InventoryItems.Count);
        Assert.DoesNotContain(updated.InventoryItems, item => item.Uid == unboundUid);
        AssertRuneMixedBoundStack(updated, boundUid, stackSize: 20, bound: 1);
        var reward = Assert.Single(updated.InventoryItems, item => item.ItemId == RuneMixedBoundRewardId);
        Assert.Equal(rewardPacket.Uid, reward.Uid);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal((ushort)1, reward.StackSize);
        var expected = original with
        {
            InventoryItems = original.InventoryItems.Where(item => item.Uid != unboundUid)
                .Concat([reward]).ToArray(),
        };
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(updated));
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));

        var reloaded = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(reloaded.InventoryItems, item => item.Uid == unboundUid);
        AssertRuneMixedBoundStack(reloaded, boundUid, stackSize: 20, bound: 1);
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(updated),
            PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
        Assert.Contains("persisted MsgItemUsage direct rune fragment use items", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune fragment use", log.ToString());
    }

    [Fact]
    public async Task HandlerRuneFragmentMixedBoundDoesNotPoolBoundsForUnboundAdmission()
    {
        const uint unboundUid = 5_337_710;
        const uint boundUid = 5_337_711;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                unboundUid, RuneMixedBoundFragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 19, bound: 0),
            PlayerItemSnapshotLoaderTests.DbItem(
                boundUid, RuneMixedBoundFragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 20, bound: 1));
        var worldData = CreateWorldDataCatalog(
            CreateRuneMixedBoundItemType(RuneMixedBoundFragmentId, "RedRuneFragment", 1, maxStackSize: 20),
            CreateRuneMixedBoundItemType(RuneMixedBoundRewardId, "RedRuneMixedBoundReward", 610, maxStackSize: 1));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var original = GetSession(handler, connection).PlayerItems;
        Assert.Equal(2, original.InventoryItems.Count);
        AssertRuneMixedBoundStack(original, unboundUid, stackSize: 19, bound: 0);
        AssertRuneMixedBoundStack(original, boundUid, stackSize: 20, bound: 1);
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var persistedBytes = File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin"));
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(unboundUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains("You don't have 20 RedRuneFragment!.", message.Content);
        var unchanged = GetSession(handler, connection).PlayerItems;
        Assert.Same(original, unchanged);
        AssertRuneMixedBoundStack(unchanged, unboundUid, stackSize: 19, bound: 0);
        AssertRuneMixedBoundStack(unchanged, boundUid, stackSize: 20, bound: 1);
        Assert.DoesNotContain(unchanged.InventoryItems, item => item.ItemId == RuneMixedBoundRewardId);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Equal(persistedBytes, File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin")));
        Assert.Equal(
            originalBytes,
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
        Assert.Contains("skipped MsgItemUsage direct rune fragment use", log.ToString());
        Assert.DoesNotContain("handled MsgItemUsage direct rune fragment use", log.ToString());
    }

    private static LegacyGameItemPacket ParseRuneMixedBoundItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertRuneMixedBoundStack(
        PlayerItemSnapshot items, uint uid, ushort stackSize, byte bound)
    {
        var item = Assert.Single(items.InventoryItems, candidate => candidate.Uid == uid);
        Assert.Equal(RuneMixedBoundFragmentId, item.ItemId);
        Assert.Equal(stackSize, item.StackSize);
        Assert.Equal(bound, item.Bound);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((uint)0, item.WarehouseId);
        Assert.Equal((ushort)1, item.Durability);
        Assert.Equal((ushort)1, item.MaximumDurability);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Locked);
        Assert.Equal((uint)0, item.PurificationItemId);
        Assert.Equal((uint)0, item.RefineryEffectId);
        Assert.Empty(item.DepositedItems);
    }

    private static ItemType CreateRuneMixedBoundItemType(
        uint itemId, string name, ushort durability, ushort maxStackSize)
    {
        var itemType = CreateItemType(itemId, name, durability);
        var fields = itemType.Fields.ToArray();
        fields[46] = maxStackSize.ToString(CultureInfo.InvariantCulture);
        return itemType with { Fields = fields };
    }
}
