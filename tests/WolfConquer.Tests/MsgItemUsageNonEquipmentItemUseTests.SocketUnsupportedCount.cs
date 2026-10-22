using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint SocketUnsupportedTargetUid = 5_348_000;

    [Fact]
    public async Task HandlerSocketUnsupportedTwoAllowedTargetAcknowledgesZero()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 2, targetItemId: 500_109, expectedResponseUid: 0);
    }

    [Fact]
    public async Task HandlerSocketUnsupportedTwoDisallowedTargetAcknowledgesZero()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 2, targetItemId: 201_003, expectedResponseUid: 0);
    }

    [Fact]
    public async Task HandlerSocketUnsupportedTwoMissingTargetAcknowledgesZero()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 2, targetItemId: null, expectedResponseUid: 0);
    }

    // Position denial sends private top-left feedback before its target-UID failure ACK.
    [Fact]
    public async Task HandlerSocketSupportedFiveDisallowedTargetRetainsTargetUid()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 5, targetItemId: 201_003, expectedResponseUid: SocketUnsupportedTargetUid);
    }

    [Fact]
    public async Task HandlerSocketSupportedFiveMissingTargetAcknowledgesZero()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 5, targetItemId: null, expectedResponseUid: 0);
    }

    private async Task AssertSocketCountRoutingAsync(
        uint declaredCount, uint? targetItemId, uint expectedResponseUid)
    {
        var (catalog, materialUids) = WriteSocketCountRoutingScenario(declaredCount, targetItemId);
        var materialItemId = declaredCount == 7 ? 1_200_006u : 1_088_000u;
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
        Assert.Equal(materialUids.Length + (targetItemId.HasValue ? 1 : 0), original.InventoryItems.Count);
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(original, uid, materialItemId, stackSize: 10);
        }
        if (targetItemId is { } itemId)
        {
            var target = Assert.Single(original.InventoryItems, item => item.Uid == SocketUnsupportedTargetUid);
            Assert.Equal(itemId, target.ItemId);
            Assert.Equal((ushort)0, target.Position);
            Assert.Equal((uint)0, target.WarehouseId);
            Assert.Equal((ushort)100, target.Durability);
            Assert.Equal((ushort)100, target.MaximumDurability);
            Assert.Equal((ushort)1, target.StackSize);
            Assert.Equal(declaredCount is 5 or 7 ? (byte)255 : (byte)0, target.SocketOne);
            Assert.Equal((byte)0, target.SocketTwo);
            Assert.Equal((byte)0, target.Bound);
            Assert.Equal((byte)0, target.Plus);
            Assert.Equal((byte)0, target.Locked);
            Assert.Empty(target.DepositedItems);
        }
        else
        {
            Assert.DoesNotContain(original.InventoryItems, item => item.Uid == SocketUnsupportedTargetUid);
        }
        Assert.Empty(original.EquipmentItems);
        Assert.Empty(original.WarehouseItems);
        Assert.Empty(original.RelicFuseItems);
        Assert.Empty(original.MythsoulBagItems);
        Assert.Empty(original.RuneItems);
        Assert.Empty(original.WardrobeItems);
        var offeredUids = materialUids.Take(checked((int)declaredCount)).ToArray();
        Assert.Equal(checked((int)declaredCount), offeredUids.Length);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(SocketUnsupportedTargetUid, declaredCount, offeredUids)));

        var outgoing = sent.Skip(countBeforeRequest).ToArray();
        var expectsPositionNotice = (declaredCount is 1 or 5 or 7 or 12) && targetItemId == 201_003;
        Assert.Equal(expectsPositionNotice ? 2 : 1, outgoing.Length);
        if (expectsPositionNotice)
        {
            AssertSocketDisallowedPositionNotice(outgoing[0]);
        }
        var responseBytes = outgoing[^1];
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(expectedResponseUid, response.Id);
        Assert.Equal(0UL, response.DwParam);
        Assert.Equal(MsgItemUsageAction.CreateSocketItem, response.Action);
        // This preserves the managed timestamp contract; native clock substitution is not proven here.
        Assert.Equal(0U, response.Timestamp);
        Assert.Equal(0U, response.DwParam2);
        Assert.Equal(0U, response.DwParam3);
        Assert.Equal(0U, response.DwParam4);

        var unchangedSession = GetSession(handler, connection);
        Assert.Same(originalSession, unchangedSession);
        Assert.Same(original, unchangedSession.PlayerItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(unchangedSession.PlayerItems));
        foreach (var uid in materialUids)
        {
            AssertSocketArgumentMaterial(unchangedSession.PlayerItems, uid, materialItemId, stackSize: 10);
        }
        Assert.Equal(baselineBytes, File.ReadAllBytes(path));
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
    }

    private (WorldDataCatalog Catalog, uint[] MaterialUids) WriteSocketCountRoutingScenario(
        uint declaredCount, uint? targetItemId)
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        var materialItemId = declaredCount == 7 ? 1_200_006u : 1_088_000u;
        var materialUids = Enumerable.Range(0, Math.Max(6, checked((int)declaredCount)))
            .Select(index => checked((uint)(5_348_100 + index))).ToArray();
        var items = new List<(PlayerItemSnapshotLoaderTests.TestDbItem Item, byte SocketOne, byte SocketTwo)>();
        if (targetItemId is { } itemId)
        {
            items.Add((PlayerItemSnapshotLoaderTests.DbItem(
                SocketUnsupportedTargetUid, itemId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1),
                declaredCount is 5 or 7 ? (byte)255 : (byte)0, 0));
        }
        items.AddRange(materialUids.Select(uid =>
            (PlayerItemSnapshotLoaderTests.DbItem(
                uid, materialItemId, position: 0, durability: 1, maximumDurability: 1, stackSize: 10),
                (byte)0, (byte)0)));
        WriteItemsWithSockets(1_293_600, items.ToArray());
        return (CreateWorldDataCatalog(
            CreateSocketArgumentItemType(
                targetItemId ?? 500_109, "SocketUnsupportedTarget", 100, maxStackSize: 1),
            CreateSocketArgumentItemType(materialItemId, "SocketUnsupportedMaterial", 1, maxStackSize: 10)),
            materialUids);
    }
}
