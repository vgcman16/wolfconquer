using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyWarehousePacketTests : IDisposable
{
    private const uint WarehouseNpcId = 65_535;
    private const uint TwinCityWarehouseNpcId = 8;
    private const uint PlayerUid = 1_293_600;
    private const uint LargeSashUid = 2001;
    private const uint LargeSashItemId = 1_100_009;
    private const uint SashItemUid = 3001;
    private const uint SashItemId = 1088001;
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-warehouse-" + Guid.NewGuid());

    [Fact]
    public void LegacyWarehouseOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2080, WorldPacketOpcodes.LegacyWarehouse);
        Assert.Equal(WorldPacketOpcodes.LegacyWarehouse, LegacyWarehousePacket.Opcode);
    }

    [Fact]
    public void LegacyWarehouseShowActionsMatchLegacySourceValues()
    {
        Assert.Equal(2560, (int)LegacyWarehouseAction.Show);
        Assert.Equal(5120, (int)LegacyWarehouseAction.Show_WH_House);
        Assert.Equal(7680, (int)LegacyWarehouseAction.ShashShow);
        Assert.Equal(7681, (int)LegacyWarehouseAction.ShashDepositItem);
        Assert.Equal(7682, (int)LegacyWarehouseAction.ShashWithdrawItem);
        Assert.Equal(10240, (int)LegacyWarehouseAction.ShowInventorySash);
    }

    [Fact]
    public void LegacyWarehouseEmptyShowResponseRoundTripsWarehouseCreateShape()
    {
        var packet = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.Show,
            ItemUid: 0,
            Capacity: 60,
            Items: []);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal("18002008ffff000000000000000a00003c00000000000000", Hex(bytes));
        Assert.Equal(WorldPacketOpcodes.LegacyWarehouse, frame.Header.Opcode);
        Assert.True(LegacyWarehousePacket.TryParse(frame, out var parsed));
        Assert.Equal(WarehouseNpcId, parsed.NpcId);
        Assert.Equal(LegacyWarehouseAction.Show, parsed.Action);
        Assert.Equal(0U, parsed.ItemUid);
        Assert.Equal(60, parsed.Capacity);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyWarehouseOneItemShowResponseWritesHeaderCountAndItemIdentity()
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 1001,
            ItemId = 1088001,
            Durability = 1,
            MaximumDurability = 1,
            StackSize = 3,
            WarehouseId = WarehouseNpcId,
        };
        var packet = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.Show,
            ItemUid: 0,
            Capacity: 60,
            Items: [item]);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal((ushort)1, reader.ReadUInt16(18));
        Assert.Equal(1001U, reader.ReadUInt32(20));
        Assert.Equal(1088001U, reader.ReadUInt32(24));
        Assert.Equal((ushort)3, reader.ReadUInt16(64));

        Assert.True(LegacyWarehousePacket.TryParse(frame, out var parsed));
        var parsedItem = Assert.Single(parsed.Items);
        Assert.Equal(1001U, parsedItem.Uid);
        Assert.Equal(1088001U, parsedItem.ItemId);
        Assert.Equal(3, parsedItem.StackSize);
    }

    [Fact]
    public async Task HandlerLegacyWarehouseShowSendsSourceOpcodeResponseForSessionWarehouseItem()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: 1001,
                    itemId: 1088001,
                    position: 0,
                    stackSize: 3,
                    warehouseId: WarehouseNpcId),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));
        var countBeforeRequest = sent.Count;
        var request = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.Show,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        var responseFrame = ReadFrame(sent[countBeforeRequest]);
        Assert.Equal(WorldPacketOpcodes.LegacyWarehouse, responseFrame.Header.Opcode);
        Assert.True(LegacyWarehousePacket.TryParse(responseFrame, out var response));
        Assert.Equal(WarehouseNpcId, response.NpcId);
        Assert.Equal(LegacyWarehouseAction.Show, response.Action);
        var responseItem = Assert.Single(response.Items);
        Assert.Equal(1001U, responseItem.Uid);
        Assert.Equal(1088001U, responseItem.ItemId);
        Assert.Equal(3, responseItem.StackSize);
        Assert.DoesNotContain("WorldServer received opcode 2080", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyWarehouseShowInventorySashTreatsLoadedInventoryAsEmptySashBeforeDeposit()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid, inventorySashCount: 300);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: 5_335_987,
                    itemId: 1_200_005,
                    position: 0,
                    stackSize: 1),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeEmptyShow = sent.Count;
        var emptyShow = new LegacyWarehousePacket(
            NpcId: PlayerUid,
            Action: LegacyWarehouseAction.ShowInventorySash,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(emptyShow.ToFrameBytes()));

        Assert.Equal(countBeforeEmptyShow, sent.Count);
        Assert.Contains("inventory sash is empty", log.ToString());
        Assert.DoesNotContain("no source items are loaded", log.ToString());

        var countBeforeDeposit = sent.Count;
        var deposit = new LegacyWarehousePacket(
            NpcId: PlayerUid,
            Action: LegacyWarehouseAction.InventorySashDepositItem,
            ItemUid: 5_335_987,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(deposit.ToFrameBytes()));

        Assert.Equal(countBeforeDeposit + 2, sent.Count);
        var usage = AssertItemUsageAction(sent[countBeforeDeposit], MsgItemUsageAction.RemoveInventory);
        Assert.Equal(5_335_987U, usage.Id);
        var depositResponse = AssertWarehouseResponse(
            sent[countBeforeDeposit + 1],
            LegacyWarehouseAction.InventorySashDepositItem);
        Assert.Equal(PlayerUid, depositResponse.NpcId);
        var depositItem = Assert.Single(depositResponse.Items);
        Assert.Equal(5_335_987U, depositItem.Uid);
        Assert.Equal(1_200_005U, depositItem.ItemId);

        var countBeforeLoadedShow = sent.Count;
        var loadedShow = emptyShow with { };

        await handler.HandleFrameAsync(connection, ReadFrame(loadedShow.ToFrameBytes()));

        Assert.Equal(countBeforeLoadedShow + 1, sent.Count);
        var showResponse = AssertWarehouseResponse(sent[countBeforeLoadedShow], LegacyWarehouseAction.ShowInventorySash);
        Assert.Equal(PlayerUid, showResponse.NpcId);
        Assert.Equal(300, showResponse.Capacity);
        var shownItem = Assert.Single(showResponse.Items);
        Assert.Equal(5_335_987U, shownItem.Uid);
        Assert.Equal(1_200_005U, shownItem.ItemId);
        Assert.DoesNotContain("no source items are loaded", log.ToString());
    }

    [Theory]
    [InlineData(WarehouseNpcId)]
    [InlineData(TwinCityWarehouseNpcId)]
    public async Task HandlerLegacyWarehouseDepositItemMovesInventoryItemIntoSessionWarehouse(uint npcId)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: 1001,
                    itemId: 1088001,
                    position: 0,
                    stackSize: 3),
                .. RareContainerItemRows(),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));
        var countBeforeDeposit = sent.Count;
        var deposit = new LegacyWarehousePacket(
            NpcId: npcId,
            Action: LegacyWarehouseAction.DepositItem,
            ItemUid: 1001,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(deposit.ToFrameBytes()));

        Assert.Equal(countBeforeDeposit + 2, sent.Count);
        AssertItemUsageAction(sent[countBeforeDeposit], MsgItemUsageAction.RemoveInventory);
        var depositResponse = AssertWarehouseResponse(sent[countBeforeDeposit + 1], LegacyWarehouseAction.DepositItem);
        Assert.Equal(npcId, depositResponse.NpcId);
        var depositItem = Assert.Single(depositResponse.Items);
        Assert.Equal(1001U, depositItem.Uid);
        Assert.Equal(1088001U, depositItem.ItemId);
        Assert.Equal(3, depositItem.StackSize);

        var countBeforeShow = sent.Count;
        var show = new LegacyWarehousePacket(
            NpcId: npcId,
            Action: LegacyWarehouseAction.Show,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(show.ToFrameBytes()));

        Assert.Equal(countBeforeShow + 1, sent.Count);
        var showResponse = AssertWarehouseResponse(sent[countBeforeShow], LegacyWarehouseAction.Show);
        var shownItem = Assert.Single(showResponse.Items);
        Assert.Equal(1001U, shownItem.Uid);
        Assert.Equal(1088001U, shownItem.ItemId);
        Assert.Equal(3, shownItem.StackSize);
        var persisted = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(PlayerUid);
        Assert.Empty(persisted.InventoryItems);
        AssertRareContainerItemsPersisted(persisted);
        var persistedWarehouseItem = Assert.Single(persisted.WarehouseItems);
        Assert.Equal(1001U, persistedWarehouseItem.Uid);
        Assert.Equal(npcId, persistedWarehouseItem.WarehouseId);
        Assert.DoesNotContain("skipped LegacyWarehouse mutation", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyWarehouseDepositItemSendsItemExtraAndLockedAfterWarehouseResponse()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: SashItemUid,
                    itemId: SashItemId,
                    position: 0,
                    stackSize: 3,
                    locked: 2,
                    unlockTimer: 20991231,
                    purificationItemId: 800001,
                    purificationLevel: 3,
                    refineryEffectId: 724440,
                    refineryEffectLevel: 2,
                    refineryEffectPercent: 10,
                    refineryEffectPercent2: 20),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeDeposit = sent.Count;
        var deposit = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.DepositItem,
            ItemUid: SashItemUid,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(deposit.ToFrameBytes()));

        Assert.Equal(countBeforeDeposit + 4, sent.Count);
        var usage = AssertItemUsageAction(sent[countBeforeDeposit], MsgItemUsageAction.RemoveInventory);
        Assert.Equal(SashItemUid, usage.Id);
        var depositResponse = AssertWarehouseResponse(sent[countBeforeDeposit + 1], LegacyWarehouseAction.DepositItem);
        Assert.Equal(WarehouseNpcId, depositResponse.NpcId);
        Assert.Equal(SashItemUid, Assert.Single(depositResponse.Items).Uid);
        AssertPermanentItemExtra(sent[countBeforeDeposit + 2], SashItemUid);
        AssertUnlockDate(sent[countBeforeDeposit + 3], SashItemUid);
    }

    [Fact]
    public async Task HandlerLegacyWarehouseWithdrawItemMovesWarehouseItemIntoSessionInventory()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: 1001,
                    itemId: 1088001,
                    position: 0,
                    stackSize: 3,
                    warehouseId: WarehouseNpcId),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));
        var countBeforeWithdraw = sent.Count;
        var withdraw = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.WithdrawItem,
            ItemUid: 1001,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(withdraw.ToFrameBytes()));

        Assert.Equal(countBeforeWithdraw + 2, sent.Count);
        AssertLegacyGameItem(sent[countBeforeWithdraw], uid: 1001, itemId: 1088001);
        var withdrawResponse = AssertWarehouseResponse(sent[countBeforeWithdraw + 1], LegacyWarehouseAction.WithdrawItem);
        Assert.Equal(WarehouseNpcId, withdrawResponse.NpcId);
        Assert.Equal(1001U, withdrawResponse.ItemUid);
        Assert.Empty(withdrawResponse.Items);
        var persistedAfterWithdraw = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(PlayerUid);
        var persistedInventoryItem = Assert.Single(persistedAfterWithdraw.InventoryItems);
        Assert.Equal(1001U, persistedInventoryItem.Uid);
        Assert.Equal(0U, persistedInventoryItem.WarehouseId);
        Assert.Empty(persistedAfterWithdraw.WarehouseItems);
        var itemFileAfterWithdraw = File.ReadAllBytes(Path.Combine(itemsRoot, "1293600.bin"));

        var countBeforeShow = sent.Count;
        var show = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.Show,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(show.ToFrameBytes()));

        Assert.Equal(countBeforeShow + 1, sent.Count);
        Assert.Equal(new LegacyWarehousePacket(WarehouseNpcId, LegacyWarehouseAction.Show, 0,
            LegacyWarehousePacket.DefaultCapacity, []).ToFrameBytes(), sent[countBeforeShow]);
        var emptyShow = AssertWarehouseResponse(sent[countBeforeShow], LegacyWarehouseAction.Show);
        Assert.Equal(WarehouseNpcId, emptyShow.NpcId);
        Assert.Equal(LegacyWarehousePacket.DefaultCapacity, emptyShow.Capacity);
        Assert.Empty(emptyShow.Items);
        Assert.Equal(itemFileAfterWithdraw, File.ReadAllBytes(Path.Combine(itemsRoot, "1293600.bin")));

        var countBeforeDeposit = sent.Count;
        var deposit = new LegacyWarehousePacket(
            NpcId: WarehouseNpcId,
            Action: LegacyWarehouseAction.DepositItem,
            ItemUid: 1001,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(deposit.ToFrameBytes()));

        Assert.Equal(countBeforeDeposit + 2, sent.Count);
        AssertItemUsageAction(sent[countBeforeDeposit], MsgItemUsageAction.RemoveInventory);
        var depositResponse = AssertWarehouseResponse(sent[countBeforeDeposit + 1], LegacyWarehouseAction.DepositItem);
        var depositItem = Assert.Single(depositResponse.Items);
        Assert.Equal(1001U, depositItem.Uid);
        Assert.Equal(1088001U, depositItem.ItemId);
        Assert.Equal(3, depositItem.StackSize);
    }

    [Theory]
    [InlineData(1_100_003, 3)]
    [InlineData(1_100_006, 6)]
    [InlineData(1_100_009, 12)]
    public async Task HandlerLegacyWarehouseShashShowUsesInventorySashUidCapacityAndDepositedItems(
        int sashItemId,
        int expectedCapacity)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: LargeSashUid,
                    itemId: (uint)sashItemId,
                    position: 0,
                    depositedItems:
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: SashItemUid,
                            itemId: SashItemId,
                            position: 0,
                            stackSize: 3,
                            purificationItemId: 800001,
                            purificationLevel: 3,
                            refineryEffectId: 724440,
                            refineryEffectLevel: 2,
                            refineryEffectPercent: 10,
                            refineryEffectPercent2: 20),
                    ]),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeRequest = sent.Count;
        var request = new LegacyWarehousePacket(
            NpcId: LargeSashUid,
            Action: LegacyWarehouseAction.ShashShow,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        var response = AssertWarehouseResponse(sent[countBeforeRequest], LegacyWarehouseAction.ShashShow);
        Assert.Equal(LargeSashUid, response.NpcId);
        Assert.Equal((ushort)expectedCapacity, response.Capacity);
        var item = Assert.Single(response.Items);
        Assert.Equal(SashItemUid, item.Uid);
        Assert.Equal(SashItemId, item.ItemId);
        Assert.Equal(3, item.StackSize);
        AssertPermanentItemExtra(sent[countBeforeRequest + 1], SashItemUid);
    }

    [Fact]
    public async Task HandlerLegacyWarehouseShashDepositItemMovesInventoryItemIntoInventorySashDeposits()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(uid: LargeSashUid, itemId: LargeSashItemId, position: 0),
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: SashItemUid,
                    itemId: SashItemId,
                    position: 0,
                    stackSize: 3,
                    locked: 2,
                    unlockTimer: 20991231,
                    purificationItemId: 800001,
                    purificationLevel: 3,
                    refineryEffectId: 724440,
                    refineryEffectLevel: 2,
                    refineryEffectPercent: 10,
                    refineryEffectPercent2: 20),
                .. RareContainerItemRows(),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeDeposit = sent.Count;
        var deposit = new LegacyWarehousePacket(
            NpcId: LargeSashUid,
            Action: LegacyWarehouseAction.ShashDepositItem,
            ItemUid: SashItemUid,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(deposit.ToFrameBytes()));

        Assert.Equal(countBeforeDeposit + 4, sent.Count);
        var usage = AssertItemUsageAction(sent[countBeforeDeposit], MsgItemUsageAction.RemoveInventory);
        Assert.Equal(SashItemUid, usage.Id);
        var depositResponse = AssertWarehouseResponse(sent[countBeforeDeposit + 1], LegacyWarehouseAction.ShashDepositItem);
        Assert.Equal(LargeSashUid, depositResponse.NpcId);
        var depositedItem = Assert.Single(depositResponse.Items);
        Assert.Equal(SashItemUid, depositedItem.Uid);
        Assert.Equal(SashItemId, depositedItem.ItemId);
        Assert.Equal(3, depositedItem.StackSize);
        AssertPermanentItemExtra(sent[countBeforeDeposit + 2], SashItemUid);
        AssertUnlockDate(sent[countBeforeDeposit + 3], SashItemUid);

        var countBeforeShow = sent.Count;
        var show = new LegacyWarehousePacket(
            NpcId: LargeSashUid,
            Action: LegacyWarehouseAction.ShashShow,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(show.ToFrameBytes()));

        Assert.Equal(countBeforeShow + 2, sent.Count);
        var showResponse = AssertWarehouseResponse(sent[countBeforeShow], LegacyWarehouseAction.ShashShow);
        var shownItem = Assert.Single(showResponse.Items);
        Assert.Equal(SashItemUid, shownItem.Uid);
        Assert.Equal(SashItemId, shownItem.ItemId);
        Assert.Equal(3, shownItem.StackSize);
        AssertPermanentItemExtra(sent[countBeforeShow + 1], SashItemUid);
        var persisted = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(PlayerUid);
        var persistedSash = Assert.Single(persisted.InventoryItems);
        Assert.Equal(LargeSashUid, persistedSash.Uid);
        AssertRareContainerItemsPersisted(persisted);
        Assert.Empty(persisted.WarehouseItems);
        var persistedDepositedItem = Assert.Single(persistedSash.DepositedItems);
        Assert.Equal(SashItemUid, persistedDepositedItem.Uid);
        Assert.Equal(SashItemId, persistedDepositedItem.ItemId);
    }

    [Fact]
    public async Task HandlerLegacyWarehouseShashWithdrawItemMovesDepositedItemBackIntoInventory()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: LargeSashUid,
                    itemId: LargeSashItemId,
                    position: 0,
                    depositedItems:
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: SashItemUid,
                            itemId: SashItemId,
                            position: 0,
                            stackSize: 3),
                    ]),
            ], []));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeWithdraw = sent.Count;
        var withdraw = new LegacyWarehousePacket(
            NpcId: LargeSashUid,
            Action: LegacyWarehouseAction.ShashWithdrawItem,
            ItemUid: SashItemUid,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(withdraw.ToFrameBytes()));

        Assert.Equal(countBeforeWithdraw + 2, sent.Count);
        AssertLegacyGameItem(sent[countBeforeWithdraw], uid: SashItemUid, itemId: SashItemId);
        var withdrawResponse = AssertWarehouseResponse(sent[countBeforeWithdraw + 1], LegacyWarehouseAction.ShashWithdrawItem);
        Assert.Equal(LargeSashUid, withdrawResponse.NpcId);
        Assert.Equal(SashItemUid, withdrawResponse.ItemUid);
        Assert.Empty(withdrawResponse.Items);

        var countBeforeShow = sent.Count;
        var show = new LegacyWarehousePacket(
            NpcId: LargeSashUid,
            Action: LegacyWarehouseAction.ShashShow,
            ItemUid: 0,
            Capacity: 0,
            Items: []);

        await handler.HandleFrameAsync(connection, ReadFrame(show.ToFrameBytes()));

        Assert.Equal(countBeforeShow + 1, sent.Count);
        var showResponse = AssertWarehouseResponse(sent[countBeforeShow], LegacyWarehouseAction.ShashShow);
        Assert.Equal(LargeSashUid, showResponse.NpcId);
        Assert.Equal(12, showResponse.Capacity);
        Assert.Empty(showResponse.Items);
        var persisted = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(PlayerUid);
        Assert.Empty(persisted.WarehouseItems);
        var persistedSash = Assert.Single(persisted.InventoryItems, item => item.Uid == LargeSashUid);
        Assert.Empty(persistedSash.DepositedItems);
        var persistedWithdrawnItem = Assert.Single(persisted.InventoryItems, item => item.Uid == SashItemUid);
        Assert.Equal(SashItemId, persistedWithdrawnItem.ItemId);
        Assert.Equal(0U, persistedWithdrawnItem.WarehouseId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static LegacyWarehousePacket AssertWarehouseResponse(byte[] bytes, LegacyWarehouseAction action)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyWarehouse, frame.Header.Opcode);
        Assert.True(LegacyWarehousePacket.TryParse(frame, out var response));
        Assert.Equal(action, response.Action);
        return response;
    }

    private static MsgItemUsagePacket AssertItemUsageAction(byte[] bytes, MsgItemUsageAction action)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.MsgItemUsage, frame.Header.Opcode);
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var packet));
        Assert.Equal(action, packet.Action);
        return packet;
    }

    private static void AssertLegacyGameItem(byte[] bytes, uint uid, uint itemId)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyGameItem, frame.Header.Opcode);
        Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
    }

    private static void AssertPermanentItemExtra(byte[] bytes, uint uid)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyItemExtra, frame.Header.Opcode);
        Assert.True(LegacyItemExtraPacket.TryParse(frame, out var packet));
        var purification = Assert.Single(packet.Purifications);
        Assert.Equal(new LegacyItemExtraPurification(
            uid,
            LegacyItemExtraPacket.StabilizationType,
            PurificationItemId: 800001,
            PurificationLevel: 3,
            EffectPercent2: 0,
            Duration: 0,
            SecondsLeft: 0), purification);
        var refinery = Assert.Single(packet.Refineries);
        Assert.Equal(new LegacyItemExtraRefinery(
            uid,
            LegacyItemExtraPacket.PermanentRefineryType,
            EffectId: 301,
            EffectLevel: 2,
            EffectPercent: 10,
            EffectPercent2: 20,
            SecondsLeft: 0), refinery);
    }

    private static void AssertUnlockDate(byte[] bytes, uint uid)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyItemLock, frame.Header.Opcode);
        Assert.True(LegacyItemLockPacket.TryParse(frame, out var packet));
        Assert.Equal(new LegacyItemLockPacket(
            uid,
            LegacyItemLockPacket.UnlockDateType,
            LegacyItemLockPacket.UnlockDateLockedMode,
            20_991_231), packet);
    }

    private static IReadOnlyList<PlayerItemSnapshotLoaderTests.TestDbItem> RareContainerItemRows()
    {
        return
        [
            PlayerItemSnapshotLoaderTests.DbItem(uid: 9001, itemId: 4100001, position: 213),
            PlayerItemSnapshotLoaderTests.DbItem(uid: 9002, itemId: 4100002, position: 217),
            PlayerItemSnapshotLoaderTests.DbItem(uid: 9003, itemId: 4030109, position: 101, runeExperience: 1234),
        ];
    }

    private static void AssertRareContainerItemsPersisted(PlayerItemSnapshot snapshot)
    {
        var relicFuse = Assert.Single(snapshot.RelicFuseItems);
        Assert.Equal(9001U, relicFuse.Uid);
        Assert.Equal((ushort)213, relicFuse.Position);

        var mythsoul = Assert.Single(snapshot.MythsoulBagItems);
        Assert.Equal(9002U, mythsoul.Uid);
        Assert.Equal((ushort)217, mythsoul.Position);

        var rune = Assert.Single(snapshot.RuneItems);
        Assert.Equal(9003U, rune.Uid);
        Assert.Equal((ushort)101, rune.Position);
        Assert.Equal(1234U, rune.RuneExperience);
    }

    private static void WriteSnapshot(string usersRoot, long accountId, long uid, ushort inventorySashCount = 0)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=7
            Activeness=0/80/0/
            InventorySashCount={{inventorySashCount}}

            [PaidNobility]
            Rank=King
            Identifier={{uid}}
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
