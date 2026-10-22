using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerSocketPersistenceOneMaterialSurplusReloadsFirstSocket()
    {
        await AssertSocketPersistenceSuccessAsync(
            materialCount: 1, materialItemId: 1_088_000, socketOne: 0,
            expectedSocketOne: 255, expectedSocketTwo: 0);
    }

    [Fact]
    public async Task HandlerSocketPersistenceFiveMaterialSurplusReloadsSecondSocket()
    {
        await AssertSocketPersistenceSuccessAsync(
            materialCount: 5, materialItemId: 1_088_000, socketOne: 255,
            expectedSocketOne: 255, expectedSocketTwo: 255);
    }

    [Fact]
    public async Task HandlerSocketPersistenceSevenMaterialSurplusReloadsSecondSocket()
    {
        await AssertSocketPersistenceSuccessAsync(
            materialCount: 7, materialItemId: 1_200_006, socketOne: 255,
            expectedSocketOne: 255, expectedSocketTwo: 255);
    }

    [Fact]
    public async Task HandlerSocketPersistenceTwelveMaterialSurplusReloadsFirstSocket()
    {
        await AssertSocketPersistenceSuccessAsync(
            materialCount: 12, materialItemId: 1_088_000, socketOne: 0,
            expectedSocketOne: 255, expectedSocketTwo: 0);
    }

    [Fact]
    public async Task HandlerSocketPersistenceDuplicateDeclaredPrefixPreservesFile()
    {
        await AssertSocketPersistenceRejectionAsync([0, 0, 1, 2, 3, 4]);
    }

    // Managed short-input safety, not equality with an unsafe native pointer read.
    [Fact]
    public async Task HandlerSocketPersistenceShortDeclaredPrefixPreservesFile()
    {
        await AssertSocketPersistenceRejectionAsync([0, 1, 2, 3]);
    }

    private async Task AssertSocketPersistenceSuccessAsync(
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
        var originalSession = GetSession(handler, connection);
        Assert.Equal(1_293_600L, originalSession.Character.Uid);
        var original = originalSession.PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var path = Path.Combine(
            _itemsRoot, originalSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        // Entry may normalize/save items, so this physical baseline is deliberately taken after entry.
        var baselineBytes = File.ReadAllBytes(path);
        Assert.Equal(originalBytes, baselineBytes);
        Assert.Equal(count + 2, original.InventoryItems.Count);
        AssertSocketPersistenceTarget(original, socketOne, socketTwo: 0);
        AssertSocketPersistenceEmptyOtherContainers(original);
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

        var updatedSession = GetSession(handler, connection);
        Assert.NotSame(originalSession, updatedSession);
        var updated = updatedSession.PlayerItems;
        Assert.NotSame(original, updated);
        Assert.Equal(original.InventoryItems.Count, updated.InventoryItems.Count);
        AssertSocketPersistenceTarget(updated, expectedSocketOne, expectedSocketTwo);
        AssertSocketPersistenceEmptyOtherContainers(updated);
        foreach (var uid in selectedUids)
        {
            AssertSocketArgumentMaterial(updated, uid, materialItemId, stackSize: 9);
        }
        AssertSocketArgumentMaterial(updated, surplusUid, materialItemId, stackSize: 10);
        Assert.Same(originalSurplus, Assert.Single(updated.InventoryItems, item => item.Uid == surplusUid));

        var finalBytes = File.ReadAllBytes(path);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated), finalBytes);
        Assert.False(baselineBytes.AsSpan().SequenceEqual(finalBytes));
        // A new loader must perform a physical read; the test never calls Save or a session callback.
        var reloaded = new PlayerItemSnapshotLoader(_itemsRoot)
            .LoadByCharacterUid(originalSession.Character.Uid);
        Assert.NotSame(updated, reloaded);
        Assert.Equal(finalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
        Assert.Equal(updated.InventoryItems.Count, reloaded.InventoryItems.Count);
        AssertSocketPersistenceTarget(reloaded, expectedSocketOne, expectedSocketTwo);
        AssertSocketPersistenceEmptyOtherContainers(reloaded);
        foreach (var uid in selectedUids)
        {
            AssertSocketArgumentMaterial(reloaded, uid, materialItemId, stackSize: 9);
        }
        AssertSocketArgumentMaterial(reloaded, surplusUid, materialItemId, stackSize: 10);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Contains("handled MsgItemUsage create socket item", log.ToString());
    }

    private async Task AssertSocketPersistenceRejectionAsync(int[] offeredIndexes)
    {
        var (catalog, materialUids) = WriteSocketArgumentScenario(
            materialEntries: 6, materialItemId: 1_088_000, socketOne: 255);
        var offeredUids = offeredIndexes.Select(index => materialUids[index]).ToArray();
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, catalog);
        var originalSession = GetSession(handler, connection);
        Assert.Equal(1_293_600L, originalSession.Character.Uid);
        var original = originalSession.PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var path = Path.Combine(
            _itemsRoot, originalSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var baselineBytes = File.ReadAllBytes(path);
        Assert.Equal(originalBytes, baselineBytes);
        Assert.Equal(7, original.InventoryItems.Count);
        AssertSocketPersistenceTarget(original, socketOne: 255, socketTwo: 0);
        AssertSocketPersistenceEmptyOtherContainers(original);
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(original, uid, 1_088_000, stackSize: 10);
        }
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(SocketArgumentTargetUid, 5, offeredUids)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                SocketArgumentTargetUid, DwParam: 0, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));
        var unchangedSession = GetSession(handler, connection);
        Assert.Same(originalSession, unchangedSession);
        Assert.Same(original, unchangedSession.PlayerItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(unchangedSession.PlayerItems));
        AssertSocketPersistenceTarget(unchangedSession.PlayerItems, socketOne: 255, socketTwo: 0);
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(unchangedSession.PlayerItems, uid, 1_088_000, stackSize: 10);
        }

        Assert.Equal(baselineBytes, File.ReadAllBytes(path));
        var reloaded = new PlayerItemSnapshotLoader(_itemsRoot)
            .LoadByCharacterUid(originalSession.Character.Uid);
        Assert.NotSame(original, reloaded);
        Assert.Equal(baselineBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
        Assert.Equal(original.InventoryItems.Count, reloaded.InventoryItems.Count);
        AssertSocketPersistenceTarget(reloaded, socketOne: 255, socketTwo: 0);
        AssertSocketPersistenceEmptyOtherContainers(reloaded);
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(reloaded, uid, 1_088_000, stackSize: 10);
        }
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.DoesNotContain("handled MsgItemUsage create socket item", log.ToString());
    }

    private static void AssertSocketPersistenceTarget(PlayerItemSnapshot items, byte socketOne, byte socketTwo)
    {
        var item = Assert.Single(items.InventoryItems, candidate => candidate.Uid == SocketArgumentTargetUid);
        Assert.Equal(SocketArgumentTargetItemId, item.ItemId);
        Assert.Equal(socketOne, item.SocketOne);
        Assert.Equal(socketTwo, item.SocketTwo);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((uint)0, item.WarehouseId);
        Assert.Equal((ushort)100, item.Durability);
        Assert.Equal((ushort)100, item.MaximumDurability);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Locked);
        Assert.Equal((uint)0, item.PurificationItemId);
        Assert.Equal((uint)0, item.RefineryEffectId);
        Assert.Empty(item.DepositedItems);
    }

    // This fixture has empty non-inventory containers; it does not prove nonempty-container preservation.
    private static void AssertSocketPersistenceEmptyOtherContainers(PlayerItemSnapshot items)
    {
        Assert.Empty(items.RelicFuseItems);
        Assert.Empty(items.MythsoulBagItems);
        Assert.Empty(items.EquipmentItems);
        Assert.Empty(items.RuneItems);
        Assert.Empty(items.WardrobeItems);
        Assert.Empty(items.WarehouseItems);
    }
}
