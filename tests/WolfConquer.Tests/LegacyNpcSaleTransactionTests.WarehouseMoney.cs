using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualSilverPickupPublishesWarehouseGoldBeforeItsUnchangedWireSequenceAndSilentReplay(
        bool capTransfer, bool alias)
    {
        const uint uid = 1_299_941;
        Seed(uid, money: capTransfer ? InventoryMoneyCap - 5 : 1_000, warehouseAlias: alias);
        var owner = NewOwner(identity: new(42));
        Player? player = null; WorldSession? before = null; FloorItemRuntimeEntry? entry = null;
        byte[]? characterBytes = null; bool armed = false; int observations = 0;
        Exception? observed = null;
        var expected = new PersistencePickupWallet(capTransfer ? InventoryMoneyCap : 1_125, 50,
            capTransfer ? 170 : 50, 444);
        player = await Login(owner, uid, _ =>
        {
            if (!armed || observations != 0) return;
            observations++;
            observed = Record.Exception(() =>
            {
                Assert.NotNull(before); Assert.NotNull(entry); Assert.NotNull(characterBytes);
                AssertPersistencePickupCommitted(owner, player!, before, entry, characterBytes, expected);
                AssertActualWarehouseWallet(owner, player!, expected.Money, expected.WarehouseMoney, alias);
            });
        });
        var native = player.Connection.NativeGameClient!;
        Assert.NotNull(native.OrdinarySpellBinding); Assert.Equal(50L, native.Player.WHMoney);
        before = Current(owner, player);
        entry = SeedPersistencePickupFloor(owner, player, disposition: 1);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        int first = player.Sent.Count; armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        Assert.Equal(1, observations); Assert.Null(observed);
        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes, expected);
        AssertActualWarehouseWallet(owner, player, expected.Money, expected.WarehouseMoney, alias);
        var frames = player.Sent.Skip(first).ToArray();
        AssertPersistencePickupFrames(frames, entry, expected);
        var golden = new List<byte[]>();
        if (capTransfer)
            golden.Add((LegacyMsgMessagePacket.SystemTalk(
                "120 Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.")
                with { ChatType = 2600, Color = LegacyMsgMessagePacket.YellowColor }).ToFrameBytes());
        var money = new LegacyMsgUpdatePacket(uid,
            [new(LegacyMsgUpdateDataType.Money, checked((ulong)expected.Money))]).ToFrameBytes();
        golden.Add(money); golden.Add(money);
        golden.Add((entry.Packet with { DropType = LegacyFloorItemDropType.Remove }).ToFrameBytes());
        golden.Add(LegacyMsgMessagePacket.SystemTalk("You have picked up a 125 silvers.").ToFrameBytes());
        Assert.Equal(golden.Count, frames.Length);
        for (var index = 0; index < golden.Count; index++) Assert.Equal(golden[index], frames[index]);
        Assert.Equal((decimal)before.Character.Money + 50 + PersistencePickupAmount,
            (decimal)expected.Money + expected.WarehouseMoney);

        await AssertPersistencePickupReplayIsSilent(owner, player);

        AssertActualWarehouseWallet(owner, player, expected.Money, expected.WarehouseMoney, alias);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualBankTransferKeepsNativeSavedAliasesAndExistingReplySequenceCoherent(
        bool withdraw, bool alias)
    {
        const uint uid = 1_299_942;
        Seed(uid, warehouseMoney: withdraw ? 350 : 50, warehouseAlias: alias);
        var owner = NewOwner(identity: new(42));
        var expectedMoney = withdraw ? 1_100L : 700L;
        var expectedWarehouse = withdraw ? 250L : 350L;
        Player? player = null; bool armed = false; int observations = 0; Exception? observed = null;
        player = await Login(owner, uid, _ =>
        {
            if (!armed) return;
            observations++;
            observed ??= Record.Exception(() =>
                AssertActualWarehouseWallet(owner, player!, expectedMoney, expectedWarehouse, alias));
        });
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        Assert.Equal(withdraw ? 350L : 50L, player.Connection.NativeGameClient.Player.WHMoney);
        var beforeItems = File.ReadAllBytes(ItemPath(uid));
        int first = player.Sent.Count; armed = true;
        var request = WarehouseGoldRequest(withdraw);

        await SendWarehouseGoldRequest(owner, player, request);

        armed = false;
        Assert.Null(observed); Assert.Equal(withdraw ? 4 : 3, observations);
        AssertActualWarehouseWallet(owner, player, expectedMoney, expectedWarehouse, alias);
        Assert.Equal(beforeItems, File.ReadAllBytes(ItemPath(uid)));
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, request, expectedMoney, expectedWarehouse);
        // This exercises the actual Store/native participant plus the real
        // single-owner Character + unchanged Items WAL before every reply.
        var show = request with { Action = MsgItemUsageAction.ShowWarehouseMoney, DwParam = 0 };
        int showFirst = player.Sent.Count;
        await SendWarehouseGoldRequest(owner, player, show);
        Assert.Equal(showFirst + 1, player.Sent.Count);
        Assert.Equal((show with { DwParam = checked((ulong)expectedWarehouse) }).ToFrameBytes(), player.Sent.Last());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBankNativeWarehouseDriftRefusesWithoutFilesOrReplyAndRestoredOwnerCanRetry(bool withdraw)
    {
        const uint uid = 1_299_943;
        var warehouse = withdraw ? 350L : 50L;
        Seed(uid, warehouseMoney: warehouse, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var before = Current(owner, player); var native = player.Connection.NativeGameClient!;
        Assert.NotNull(native.OrdinarySpellBinding); Assert.Equal(warehouse, native.Player.WHMoney);
        var characterBytes = File.ReadAllBytes(UserPath(uid)); var itemsBytes = File.ReadAllBytes(ItemPath(uid));
        int first = player.Sent.Count;
        native.Player.WHMoney = checked(warehouse + 1);
        try
        {
            await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw));

            Assert.Same(before, Current(owner, player)); Assert.Equal(first, player.Sent.Count);
            Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
            Assert.Equal(itemsBytes, File.ReadAllBytes(ItemPath(uid)));
            Assert.Equal(checked(warehouse + 1), native.Player.WHMoney);
            Assert.Equal(1_000L, native.Player.Money); Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        }
        finally { native.Player.WHMoney = warehouse; }

        await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw));

        var money = withdraw ? 1_100L : 700L; var afterWarehouse = withdraw ? 250L : 350L;
        AssertActualWarehouseWallet(owner, player, money, afterWarehouse, alias: true);
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, WarehouseGoldRequest(withdraw), money, afterWarehouse);
    }

    [Fact]
    public async Task ActualBankWithdrawalChecksFinalWarehouseCapacityAfterReturningInventoryOverflow()
    {
        const uint uid = 1_299_944;
        Seed(uid, money: InventoryMoneyCap, warehouseMoney: long.MaxValue, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        Player? player = null; bool armed = false; int observations = 0; Exception? observed = null;
        player = await Login(owner, uid, _ =>
        {
            if (!armed) return;
            observations++;
            observed ??= Record.Exception(() =>
                AssertActualWarehouseWallet(owner, player!, InventoryMoneyCap, long.MaxValue, alias: true));
        });
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        AssertActualWarehouseWallet(owner, player, InventoryMoneyCap, long.MaxValue, alias: true);
        var before = Current(owner, player); int first = player.Sent.Count;
        var items = File.ReadAllBytes(ItemPath(uid)); armed = true;

        await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw: true));

        armed = false; Assert.Null(observed); Assert.Equal(4, observations);
        AssertActualWarehouseWallet(owner, player, InventoryMoneyCap, long.MaxValue, alias: true);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, WarehouseGoldRequest(withdraw: true),
            InventoryMoneyCap, long.MaxValue);
    }

    [Fact]
    public async Task ActualBankDepositBeyondSignedWarehouseCapacityRefusesWithoutDebitingOrPublishing()
    {
        const uint uid = 1_299_945;
        Seed(uid, warehouseMoney: long.MaxValue, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid); var before = Current(owner, player);
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        AssertActualWarehouseWallet(owner, player, 1_000, long.MaxValue, alias: true);
        var character = File.ReadAllBytes(UserPath(uid)); var items = File.ReadAllBytes(ItemPath(uid));
        int first = player.Sent.Count;

        await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw: false));

        Assert.Same(before, Current(owner, player)); Assert.Equal(first, player.Sent.Count);
        Assert.Equal(character, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        AssertActualWarehouseWallet(owner, player, 1_000, long.MaxValue, alias: true);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void AssertActualWarehouseWallet(Owner owner, Player player, long money, long warehouse, bool alias)
    {
        var current = Current(owner, player).Character;
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(UserPath(player.Uid)), out _));
        var native = player.Connection.NativeGameClient!;
        Assert.Equal((money, 50L, 333L, 444L),
            (current.Money, current.ConquerPoints, current.BoundConquerPoints, current.BankConquerPoints));
        Assert.Equal((money, 50L, 333L, 444L),
            (saved.Money, saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
        Assert.Equal((money, warehouse, 50U, 333, 444UL),
            (native.Player.Money, native.Player.WHMoney, native.Player.ConquerPoints,
                native.Player.BoundConquerPoints, native.ConquerPointsSave));
        foreach (var attributes in new[] { current.Attributes, saved.Attributes })
        {
            Assert.Equal(warehouse.ToString(CultureInfo.InvariantCulture), attributes["WHMoney"]);
            Assert.Equal(alias, attributes.ContainsKey("WarehouseMoney"));
            if (alias) Assert.Equal(attributes["WHMoney"], attributes["WarehouseMoney"]);
        }
    }

    private static MsgItemUsagePacket WarehouseGoldRequest(bool withdraw) => new(12345, withdraw ? 100UL : 300UL,
        withdraw ? MsgItemUsageAction.WarehouseWithdraw : MsgItemUsageAction.DepositWarehouse, 112233, 44, 55, 66);

    private static Task SendWarehouseGoldRequest(Owner owner, Player player, MsgItemUsagePacket request) =>
        owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(request.ToFrameBytes()), player.Lifetime.Token).AsTask();

    private static void AssertWarehouseGoldFrames(byte[][] actual, uint uid, MsgItemUsagePacket request,
        long money, long warehouse)
    {
        bool withdraw = request.Action == MsgItemUsageAction.WarehouseWithdraw;
        Assert.Equal(withdraw ? 4 : 3, actual.Length);
        var moneyFrame = new LegacyMsgUpdatePacket(uid,
            [new(LegacyMsgUpdateDataType.Money, checked((ulong)money))]).ToFrameBytes();
        Assert.Equal(moneyFrame, actual[0]); Assert.Equal(moneyFrame, actual[1]);
        if (withdraw) Assert.Equal(new LegacyMsgUpdatePacket(uid,
            [new(LegacyMsgUpdateDataType.WarehouseMoney, checked((ulong)warehouse))]).ToFrameBytes(), actual[2]);
        Assert.Equal(request.ToFrameBytes(), actual[^1]);
    }
}
