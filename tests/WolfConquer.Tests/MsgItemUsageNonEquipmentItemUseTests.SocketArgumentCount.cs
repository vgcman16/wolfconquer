using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint SocketArgumentTargetUid = 5_347_000;
    private const uint SocketArgumentTargetItemId = 500_109;

    [Fact]
    public async Task HandlerSocketArgumentCountOneIgnoresSurplusAndOpensFirstSocket()
    {
        await AssertSocketArgumentSurplusSuccessAsync(
            materialCount: 1, materialItemId: 1_088_000, socketOne: 0,
            expectedSocketOne: 255, expectedSocketTwo: 0);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountFiveIgnoresSurplusAndOpensSecondSocket()
    {
        await AssertSocketArgumentSurplusSuccessAsync(
            materialCount: 5, materialItemId: 1_088_000, socketOne: 255,
            expectedSocketOne: 255, expectedSocketTwo: 255);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountSevenIgnoresSurplusAndOpensSecondSocket()
    {
        await AssertSocketArgumentSurplusSuccessAsync(
            materialCount: 7, materialItemId: 1_200_006, socketOne: 255,
            expectedSocketOne: 255, expectedSocketTwo: 255);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountTwelveIgnoresSurplusAndOpensFirstSocket()
    {
        await AssertSocketArgumentSurplusSuccessAsync(
            materialCount: 12, materialItemId: 1_088_000, socketOne: 0,
            expectedSocketOne: 255, expectedSocketTwo: 0);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountDuplicateDeclaredUidCannotUseValidSurplus()
    {
        await AssertSocketArgumentRejectionAsync(5, [0, 0, 1, 2, 3, 4]);
    }

    // These protect managed safety/default behavior, not unsafe-native input equality.
    [Fact]
    public async Task HandlerSocketArgumentCountShortDeclaredPrefixRejectsWithoutMutation()
    {
        await AssertSocketArgumentRejectionAsync(5, [0, 1, 2, 3]);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountZeroPreservesUnsupportedRejection()
    {
        await AssertSocketArgumentRejectionAsync(0, [0, 1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountFiftyPreservesUnsupportedRejection()
    {
        await AssertSocketArgumentRejectionAsync(50, [0, 1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task HandlerSocketArgumentCountUIntMaxPreservesUnsupportedRejection()
    {
        await AssertSocketArgumentRejectionAsync(uint.MaxValue, [0, 1, 2, 3, 4, 5]);
    }

    private async Task AssertSocketArgumentSurplusSuccessAsync(
        uint materialCount,
        uint materialItemId,
        byte socketOne,
        byte expectedSocketOne,
        byte expectedSocketTwo)
    {
        var count = checked((int)materialCount);
        var (catalog, materialUids) = WriteSocketArgumentScenario(count + 1, materialItemId, socketOne);
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, catalog);
        var original = GetSession(handler, connection).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var originalTarget = Assert.Single(original.InventoryItems, item => item.Uid == SocketArgumentTargetUid);
        Assert.Equal(socketOne, originalTarget.SocketOne);
        Assert.Equal((byte)0, originalTarget.SocketTwo);
        Assert.Equal(count + 2, original.InventoryItems.Count);
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(original, uid, materialItemId, stackSize: 10);
        }
        var selectedUids = materialUids.Take(count).ToArray();
        var surplusUid = materialUids[^1];
        var originalSurplus = Assert.Single(original.InventoryItems, item => item.Uid == surplusUid);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(SocketArgumentTargetUid, materialCount, materialUids)));

        var outgoing = sent.Skip(countBeforeRequest).ToArray();
        Assert.Equal(count + 2, outgoing.Length);
        var targetPacket = ParseSocketArgumentItem(outgoing[0]);
        Assert.Equal(SocketArgumentTargetUid, targetPacket.Uid);
        Assert.Equal(SocketArgumentTargetItemId, targetPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, targetPacket.Mode);
        Assert.Equal(expectedSocketOne, targetPacket.SocketOne);
        Assert.Equal(expectedSocketTwo, targetPacket.SocketTwo);
        var updates = outgoing.Skip(1).Take(count).Select(ParseSocketArgumentItem).ToArray();
        Assert.Equal(
            selectedUids.OrderBy(uid => uid),
            updates.Select(packet => packet.Uid).OrderBy(uid => uid));
        Assert.All(updates, packet =>
        {
            Assert.Equal(materialItemId, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
            Assert.Equal((ushort)9, packet.StackSize);
            Assert.Equal((byte)0, packet.Bound);
        });
        Assert.DoesNotContain(updates, packet => packet.Uid == surplusUid);
        Assert.DoesNotContain(outgoing, bytes =>
            MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage) &&
            usage.Action == MsgItemUsageAction.RemoveInventory);
        AssertItemUsage(
            outgoing[^1],
            new MsgItemUsagePacket(
                SocketArgumentTargetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var updated = GetSession(handler, connection).PlayerItems;
        Assert.NotSame(original, updated);
        Assert.Equal(original.InventoryItems.Count, updated.InventoryItems.Count);
        var target = Assert.Single(updated.InventoryItems, item => item.Uid == SocketArgumentTargetUid);
        Assert.Equal(SocketArgumentTargetItemId, target.ItemId);
        Assert.Equal(expectedSocketOne, target.SocketOne);
        Assert.Equal(expectedSocketTwo, target.SocketTwo);
        foreach (var uid in selectedUids)
        {
            AssertSocketArgumentMaterial(updated, uid, materialItemId, stackSize: 9);
        }
        AssertSocketArgumentMaterial(updated, surplusUid, materialItemId, stackSize: 10);
        Assert.Same(originalSurplus, Assert.Single(updated.InventoryItems, item => item.Uid == surplusUid));
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Contains("handled MsgItemUsage create socket item", log.ToString());
    }

    private async Task AssertSocketArgumentRejectionAsync(uint declaredCount, int[] offeredIndexes)
    {
        var (catalog, materialUids) = WriteSocketArgumentScenario(
            materialEntries: 6, materialItemId: 1_088_000, socketOne: 255);
        var offeredUids = offeredIndexes.Select(index => materialUids[index]).ToArray();
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, catalog);
        var original = GetSession(handler, connection).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(SocketArgumentTargetUid, declaredCount, offeredUids)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                declaredCount is 1 or 5 or 7 or 12 ? SocketArgumentTargetUid : 0u,
                DwParam: 0, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));
        var unchanged = GetSession(handler, connection).PlayerItems;
        Assert.Same(original, unchanged);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(unchanged));
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(unchanged, uid, 1_088_000, stackSize: 10);
        }
        var target = Assert.Single(unchanged.InventoryItems, item => item.Uid == SocketArgumentTargetUid);
        Assert.Equal((byte)255, target.SocketOne);
        Assert.Equal((byte)0, target.SocketTwo);
        Assert.DoesNotContain("handled MsgItemUsage create socket item", log.ToString());
        if (declaredCount is not (1 or 5 or 7 or 12))
        {
            Assert.Contains(
                $"itemUid={SocketArgumentTargetUid} dwParam2={declaredCount}; unsupported legacy CreateSocketItem material count.",
                log.ToString());
        }
    }

    private (WorldDataCatalog Catalog, uint[] MaterialUids) WriteSocketArgumentScenario(
        int materialEntries, uint materialItemId, byte socketOne)
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        var materialUids = Enumerable.Range(0, materialEntries)
            .Select(index => checked((uint)(5_347_100 + index))).ToArray();
        var items = new List<(PlayerItemSnapshotLoaderTests.TestDbItem Item, byte SocketOne, byte SocketTwo)>
        {
            (PlayerItemSnapshotLoaderTests.DbItem(
                SocketArgumentTargetUid, SocketArgumentTargetItemId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1), socketOne, 0),
        };
        items.AddRange(materialUids.Select(uid =>
            (PlayerItemSnapshotLoaderTests.DbItem(
                uid, materialItemId, position: 0, durability: 1, maximumDurability: 1, stackSize: 10),
                (byte)0, (byte)0)));
        WriteItemsWithSockets(1_293_600, items.ToArray());
        return (CreateWorldDataCatalog(
            CreateSocketArgumentItemType(SocketArgumentTargetItemId, "SocketArgumentTarget", 100, maxStackSize: 1),
            CreateSocketArgumentItemType(materialItemId, "SocketArgumentMaterial", 1, maxStackSize: 10)),
            materialUids);
    }

    private static void AssertSocketArgumentMaterial(
        PlayerItemSnapshot items, uint uid, uint itemId, ushort stackSize)
    {
        var item = Assert.Single(items.InventoryItems, candidate => candidate.Uid == uid);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal(stackSize, item.StackSize);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((uint)0, item.WarehouseId);
        Assert.Equal((ushort)1, item.Durability);
        Assert.Equal((ushort)1, item.MaximumDurability);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Locked);
        Assert.Equal((uint)0, item.PurificationItemId);
        Assert.Equal((uint)0, item.RefineryEffectId);
        Assert.Empty(item.DepositedItems);
    }

    private static LegacyGameItemPacket ParseSocketArgumentItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static ItemType CreateSocketArgumentItemType(
        uint itemId, string name, ushort durability, ushort maxStackSize)
    {
        var itemType = CreateItemType(itemId, name, durability);
        var fields = itemType.Fields.ToArray();
        fields[46] = maxStackSize.ToString(CultureInfo.InvariantCulture);
        return itemType with { Fields = fields };
    }
}
