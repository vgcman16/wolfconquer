using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Managed D13 handlers, explicit native-owner fixtures, and disposable real files;
// not actual-client, native transport-birth, SQL, or hardware power-loss proof.
public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData((ushort)10, 300L)]
    [InlineData((ushort)5, 150L)]
    public async Task SalePersistsBothImagesAndPublishesSessionBeforeFirstRemovalThenCannotPayAgain(ushort durability, long value)
    {
        const uint uid = 1_299_001;
        Seed(uid, durability);
        var owner = NewOwner();
        Player? player = null;
        var armed = false;
        var observedRemovals = 0;
        Exception? callbackFailure = null;
        WorldSession? expected = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || !IsRemoval(bytes)) return;
            observedRemovals++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(expected);
                AssertSaved(uid, expected);
                Assert.Equal(expected.Character.Money, Current(owner, player!).Character.Money);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems),
                    PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player!).PlayerItems));
                Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            });
        });
        expected = SaleResult(Current(owner, player), 1_000 + value, 50);
        var first = player.Sent.Count;
        armed = true;

        await Sell(owner, player);

        Assert.Equal(1, observedRemovals);
        // Dispatch catches socket-delegate exceptions. Retain the observation
        // and assert it here so an ordering regression cannot be swallowed.
        Assert.Null(callbackFailure);
        AssertSaved(uid, expected);
        Assert.Equal(expected.Character.Money, Current(owner, player).Character.Money);
        Assert.Equal(KeptUid, Assert.Single(Current(owner, player).PlayerItems.InventoryItems).Uid);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
        Assert.Equal(1, observedRemovals);
    }

    [WindowsNpcSaleTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LockedCharacterOrItemsCannotPublishPartialSaleAndUnlockedRetryPaysExactlyOnce(bool itemTarget, bool allowRead)
    {
        const uint uid = 1_299_011;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var userBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        var path = itemTarget ? ItemPath(uid) : UserPath(uid);
        using (File.Open(path, FileMode.Open, FileAccess.Read, allowRead ? FileShare.Read : FileShare.None))
        {
            // FileShare.Read reaches the real Windows replacement failure, not
            // merely a failed baseline read. FileShare.None is the earlier gate.
            await Sell(owner, player);
            Assert.Same(before, Current(owner, player));
            Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);
        }
        Assert.Equal(userBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        await Sell(owner, player);

        AssertSaved(uid, SaleResult(before, 1_300, 50));
        Assert.Equal(1_300, Current(owner, player).Character.Money);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    [Theory]
    [InlineData("character-uid")]
    [InlineData("money")]
    [InlineData("warehouse")]
    [InlineData("pin")]
    [InlineData("item-bytes")]
    [InlineData("inventory-duplicate")]
    [InlineData("warehouse-duplicate")]
    [InlineData("nested-duplicate")]
    public async Task StaleDiskBaselinesOrGloballyAmbiguousItemUidNeverRemoveOrPay(string scenario)
    {
        const uint uid = 1_299_021;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var current = Current(owner, player);
        var sold = current.PlayerItems.InventoryItems.Single(item => item.Uid == SoldUid);
        switch (scenario)
        {
            case "character-uid": ReplaceCharacterField(uid, "UID", (uid + 1).ToString(CultureInfo.InvariantCulture)); break;
            case "money": ReplaceCharacterField(uid, "Money", "1234"); break;
            case "warehouse": ReplaceCharacterField(uid, "WHMoney", "999"); break;
            case "pin": ReplaceCharacterField(uid, "SecurityPass", "111,0,0"); break;
            case "item-bytes":
                new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, current.PlayerItems with
                {
                    InventoryItems = current.PlayerItems.InventoryItems.Select(item => item.Uid == SoldUid
                        ? item.WithItemId(ItemId + 1) : item).ToArray(),
                });
                break;
            default:
                var items = scenario switch
                {
                    "inventory-duplicate" => current.PlayerItems with { InventoryItems = current.PlayerItems.InventoryItems.Append(sold).ToArray() },
                    "warehouse-duplicate" => current.PlayerItems with { WarehouseItems = current.PlayerItems.WarehouseItems.Append(sold.WithWarehouseId(1000, 0)).ToArray() },
                    _ => current.PlayerItems with
                    {
                        WarehouseItems = current.PlayerItems.WarehouseItems.Append(new PlayerItemSnapshotItem
                        {
                            Uid = KeptUid + 2, ItemId = ItemId, WarehouseId = 1000, StackSize = 1, DepositedItems = [sold],
                        }).ToArray(),
                    },
                };
                owner.Sessions.Set(player.Connection, current with { PlayerItems = items });
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items), File.ReadAllBytes(ItemPath(uid)));
                break;
        }

        await AssertDenied(owner, player);

        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Theory]
    [InlineData((ushort)0, (ushort)10)]
    [InlineData((ushort)11, (ushort)10)]
    [InlineData((ushort)10, (ushort)0)]
    [InlineData((ushort)0, (ushort)0)]
    public async Task InvalidOrZeroDurabilityRemovesOnlyTheItemWithoutMoneyNotifications(ushort durability, ushort maximum)
    {
        const uint uid = 1_299_031;
        Seed(uid, durability, maximum);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var expected = SaleResult(Current(owner, player), 1_000, 50);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Equal(1_000, Current(owner, player).Character.Money);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(0, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    [Fact]
    public async Task ValidDurabilityAndZeroWorthStillEmitsBothNativeMoneySetterNotifications()
    {
        const uint uid = 1_299_035;
        Seed(uid);
        var owner = NewOwner(goldWorth: 0);
        var player = await Login(owner, uid);
        var expected = SaleResult(Current(owner, player), 1_000, 50);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    [Fact]
    public async Task InventoryCapTransfersOnlyOverflowToWarehouseAndUpdatesBothExistingAliases()
    {
        const uint uid = 1_299_041;
        Seed(uid, money: InventoryMoneyCap - 100, warehouseMoney: 50, warehouseAlias: true);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var expected = SaleResult(Current(owner, player), InventoryMoneyCap, 250);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Equal(InventoryMoneyCap, Current(owner, player).Character.Money);
        Assert.Equal("250", Current(owner, player).Character.Attributes["WHMoney"]);
        Assert.Equal("250", Current(owner, player).Character.Attributes["WarehouseMoney"]);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    [Fact]
    public async Task NativeSignedWarehouseOverflowCannotDestroyItemOrPublishPayment()
    {
        const uint uid = 1_299_051;
        Seed(uid, money: InventoryMoneyCap - 100, warehouseMoney: long.MaxValue);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        await AssertDenied(owner, player);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private async Task AssertReplayCannotPay(Owner owner, Player player)
    {
        var session = Current(owner, player);
        var userBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var first = player.Sent.Count;
        await Sell(owner, player);
        Assert.Same(session, Current(owner, player));
        Assert.Equal(userBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);
    }

    private static int MoneyUpdateCount(Player player, int first) => player.Sent.Skip(first).Count(bytes =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Money));

    private void ReplaceCharacterField(uint uid, string key, string value) => File.WriteAllLines(UserPath(uid),
        File.ReadAllLines(UserPath(uid)).Select(line => line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)
            ? key + "=" + value : line));
}
