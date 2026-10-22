using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneDebitFragmentId = 3_306_366;
    private const uint RuneDebitRewardId = 4_012_001;

    [Fact]
    public async Task HandlerRuneFragmentTwoStacksReceiveTenSeparateDebitsEachAndPersist()
    {
        const uint firstUid = 5_337_600;
        const uint secondUid = 5_337_601;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                firstUid, RuneDebitFragmentId, position: 0, durability: 1, maximumDurability: 1, stackSize: 20),
            PlayerItemSnapshotLoaderTests.DbItem(
                secondUid, RuneDebitFragmentId, position: 0, durability: 1, maximumDurability: 1, stackSize: 20));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(RuneDebitFragmentId, "RedRuneFragment", durability: 1),
            CreateItemType(RuneDebitRewardId, "RedRuneDebitReward", durability: 610));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var originalItems = GetSession(handler, connection).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalItems);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(firstUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 22, sent.Count);
        var materialUpdates = sent.Skip(countBeforeRequest).Take(20)
            .Select(ParseRuneDebitUpdate).ToArray();
        Assert.All(materialUpdates, packet =>
        {
            Assert.Equal(RuneDebitFragmentId, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
            Assert.Equal((byte)0, packet.Bound);
        });
        foreach (var uid in new[] { firstUid, secondUid })
        {
            var perUid = materialUpdates.Where(packet => packet.Uid == uid).ToArray();
            Assert.Equal(
                Enumerable.Range(10, 10).Reverse().Select(stack => checked((ushort)stack)),
                perUid.Select(packet => packet.StackSize));
        }

        AssertRuneDebitRewardAndMessage(sent[countBeforeRequest + 20], sent[countBeforeRequest + 21]);
        var updatedItems = GetSession(handler, connection).PlayerItems;
        Assert.Equal(3, updatedItems.InventoryItems.Count);
        foreach (var uid in new[] { firstUid, secondUid })
        {
            var item = Assert.Single(updatedItems.InventoryItems, candidate => candidate.Uid == uid);
            Assert.Equal((ushort)10, item.StackSize);
        }
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalItems));
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
        Assert.Contains("persisted MsgItemUsage direct rune fragment use items", log.ToString());
    }

    [Fact]
    public async Task HandlerRuneFragmentSingleStackTwentyFiveEmitsEveryDebitThroughFiveAndPersists()
    {
        const uint fragmentUid = 5_337_610;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                fragmentUid, RuneDebitFragmentId, position: 0, durability: 1, maximumDurability: 1, stackSize: 25));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(RuneDebitFragmentId, "RedRuneFragment", durability: 1),
            CreateItemType(RuneDebitRewardId, "RedRuneDebitReward", durability: 610));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 22, sent.Count);
        var updates = sent.Skip(countBeforeRequest).Take(20).Select(ParseRuneDebitUpdate).ToArray();
        Assert.Equal(
            Enumerable.Range(5, 20).Reverse().Select(stack => checked((ushort)stack)),
            updates.Select(packet => packet.StackSize));
        Assert.All(updates, packet =>
        {
            Assert.Equal(fragmentUid, packet.Uid);
            Assert.Equal(RuneDebitFragmentId, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        });
        AssertRuneDebitRewardAndMessage(sent[countBeforeRequest + 20], sent[countBeforeRequest + 21]);
        var updatedItems = GetSession(handler, connection).PlayerItems;
        var fragment = Assert.Single(updatedItems.InventoryItems, item => item.Uid == fragmentUid);
        Assert.Equal((ushort)5, fragment.StackSize);
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
    }

    [Fact]
    public async Task HandlerRuneFragmentNineteenUnitsRejectWithoutDebitingOrRewarding()
    {
        const uint fragmentUid = 5_337_620;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                fragmentUid, RuneDebitFragmentId, position: 0, durability: 1, maximumDurability: 1, stackSize: 19));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(RuneDebitFragmentId, "RedRuneFragment", durability: 1),
            CreateItemType(RuneDebitRewardId, "RedRuneDebitReward", durability: 610));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var originalItems = GetSession(handler, connection).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalItems);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains("You don't have 20 RedRuneFragment!.", message.Content);
        Assert.Same(originalItems, GetSession(handler, connection).PlayerItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalItems));
        Assert.Equal(
            originalBytes,
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
        Assert.DoesNotContain(originalItems.InventoryItems, item => item.ItemId == RuneDebitRewardId);
    }

    [Fact]
    public async Task HandlerRuneFragmentConsumptionPreservesOriginalMetadataAndUnrelatedContainers()
    {
        const uint fragmentUid = 5_337_630;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        var fragment = new PlayerItemSnapshotItem
        {
            Uid = fragmentUid,
            ItemId = RuneDebitFragmentId,
            Position = 0,
            Durability = 1,
            MaximumDurability = 1,
            StackSize = 25,
            Bound = 0,
            Plus = 0,
            Locked = 0,
            Color = 4,
            Enchant = 3,
            OwnerUid = 42,
            OwnerName = "debit owner",
            Signature = "retain metadata",
        };
        var unrelated = RuneDebitUnrelatedItem(5_337_631, 1_000_000, 0, stackSize: 7);
        var seed = new PlayerItemSnapshot(
            [fragment, unrelated],
            [RuneDebitUnrelatedItem(5_337_632, 4_100_001, 213)],
            [RuneDebitUnrelatedItem(5_337_633, 3_307_286, 217)],
            [RuneDebitUnrelatedItem(5_337_634, 410_009, 4)],
            [RuneDebitUnrelatedItem(5_337_635, 4_040_001, 211)],
            [new PlayerItemSnapshotItem
            {
                Uid = 5_337_636, ItemId = 181_305, Position = 0, WarehouseId = 100,
                Durability = 1, MaximumDurability = 1, StackSize = 1,
            }])
        {
            WarehouseItems = [new PlayerItemSnapshotItem
            {
                Uid = 5_337_637, ItemId = 100_000, Position = 0, WarehouseId = 1002,
                Durability = 1, MaximumDurability = 1, StackSize = 1,
            }],
        };
        new PlayerItemSnapshotLoader(_itemsRoot).SaveByCharacterUid(1_293_600, seed);
        var worldData = CreateWorldDataCatalog(
            CreateItemType(RuneDebitFragmentId, "RedRuneFragment", durability: 1),
            CreateItemType(RuneDebitRewardId, "RedRuneDebitReward", durability: 610),
            CreateItemType(1_000_000, "UnrelatedPotion", durability: 1),
            CreateItemType(4_100_001, "UnrelatedRelic", durability: 1),
            CreateItemType(3_307_286, "UnrelatedMythsoul", durability: 1),
            CreateItemType(410_009, "UnrelatedWeapon", durability: 1),
            CreateItemType(4_040_001, "UnrelatedRuneEssence", durability: 1),
            CreateItemType(181_305, "UnrelatedWardrobe", durability: 1),
            CreateItemType(100_000, "UnrelatedWarehouse", durability: 1));
        using var log = new StringWriter();
        var (handler, connection, _) = await EnterWorldAsync(log, worldData);
        var original = GetSession(handler, connection).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var originalFragment = Assert.Single(original.InventoryItems, item => item.Uid == fragmentUid);
        var originalUnrelated = Assert.Single(original.InventoryItems, item => item.Uid == unrelated.Uid);

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        var updated = GetSession(handler, connection).PlayerItems;
        Assert.NotSame(original, updated);
        Assert.Same(originalUnrelated, Assert.Single(updated.InventoryItems, item => item.Uid == unrelated.Uid));
        Assert.Same(original.RelicFuseItems, updated.RelicFuseItems);
        Assert.Same(original.MythsoulBagItems, updated.MythsoulBagItems);
        Assert.Same(original.EquipmentItems, updated.EquipmentItems);
        Assert.Same(original.RuneItems, updated.RuneItems);
        Assert.Same(original.WardrobeItems, updated.WardrobeItems);
        Assert.Same(original.WarehouseItems, updated.WarehouseItems);
        var updatedFragment = Assert.Single(updated.InventoryItems, item => item.Uid == fragmentUid);
        Assert.Equal((ushort)5, updatedFragment.StackSize);
        var reward = Assert.Single(updated.InventoryItems, item => item.ItemId == RuneDebitRewardId);
        Assert.Equal((ushort)610, reward.Durability);
        Assert.Equal((byte)0, reward.Bound);
        var expected = original with
        {
            InventoryItems = original.InventoryItems
                .Select(item => item.Uid == fragmentUid ? originalFragment.WithStackSize(5) : item)
                .Concat([reward]).ToArray(),
        };
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(updated));
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(updated),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
    }

    private static LegacyGameItemPacket ParseRuneDebitUpdate(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertRuneDebitRewardAndMessage(byte[] rewardFrame, byte[] messageFrame)
    {
        var reward = ParseRuneDebitUpdate(rewardFrame);
        Assert.Equal(RuneDebitRewardId, reward.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
        Assert.Equal((ushort)610, reward.Durability);
        Assert.Equal((ushort)610, reward.MaximumDurability);
        Assert.Equal((byte)0, reward.Bound);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(messageFrame), out var message));
        Assert.Contains("You have got 1 RedRuneDebitReward!.", message.Content);
    }

    private static PlayerItemSnapshotItem RuneDebitUnrelatedItem(
        uint uid, uint itemId, ushort position, ushort stackSize = 1) =>
        new()
        {
            Uid = uid, ItemId = itemId, Position = position, StackSize = stackSize,
            Durability = 1, MaximumDurability = 1,
        };
}
