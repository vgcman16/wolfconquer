using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Theory]
    [InlineData(1000L, 125L, 9L)]
    [InlineData(1000L, -125L, 9L)]
    public Task NativeTradeMoneyWithoutDurableEscrowPreservesBothActualOwners(
        long balance, long amount, long escrow) => NativePacketTestThread.Run(() =>
            AssertUnboundNativeTradeMoney("pair", balance, amount, escrow));

    [Theory]
    [InlineData("nullRequest")]
    [InlineData("nullPacket")]
    public Task NativeTradeMoneyNullArgumentsRefuseWithoutTheFormerDebitPrefix(string shape) =>
        NativePacketTestThread.Run(() => AssertUnboundNativeTradeMoney(shape, 1000, 125, 9));

    private static unsafe void AssertUnboundNativeTradeMoney(string shape, long balance, long amount, long escrow)
    {
        using var scope = new Scope();
        // Actual native objects without an accepting transport or persistence substitute.
        var owner = new LegacyNativeGameClientRuntime();
        var target = new LegacyNativeGameClientRuntime();
        var player = owner.Player;
        var peerPlayer = target.Player;
        var wallet = typeof(LegacyNativePlayerRuntime).GetField("_Money", BindingFlags.Instance | BindingFlags.NonPublic)!;
        wallet.SetValue(player, balance);
        wallet.SetValue(peerPlayer, 77L);
        player.WHMoney = 31;
        peerPlayer.WHMoney = 32;
        player.targetTrade = 18;
        peerPlayer.targetTrade = 17;
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        target.Inventory = new LegacyNativeInventoryRuntime(target);
        var item = Item(121);
        var peerItem = Item(122);
        owner.Inventory.ClientItems.TryAdd(item.Uid, item);
        target.Inventory.ClientItems.TryAdd(peerItem.Uid, peerItem);
        var trade = new LegacyNativeTradeRuntime(owner)
            { Target = target, WindowOpen = true, Confirmed = true, Money = escrow, ConquerPoints = 14 };
        var peerTrade = new LegacyNativeTradeRuntime(target)
            { Target = owner, WindowOpen = true, Confirmed = true, Money = 19, ConquerPoints = 15 };
        trade.Items.TryAdd(item.Uid, item);
        peerTrade.Items.TryAdd(peerItem.Uid, peerItem);
        owner.MyTrade = trade;
        target.MyTrade = peerTrade;
        var ownerPlayer = owner.Player;
        var ownerInventory = owner.Inventory;
        var ownerMap = ownerInventory?.ClientItems;
        var peerInventory = target.Inventory!;
        var peerMap = peerInventory.ClientItems;
        var ownerTrade = owner.MyTrade;
        var tradeTarget = trade.Target;
        var peerTarget = peerTrade.Target;
        bool peerWindow = peerTrade.WindowOpen;
        var request = new LegacyNativeMsgTrade.Trade
            { Action = 7, Money = amount, ItemUID = 123, CPS = 4, Unknown = 5, UID = 6 };
        byte[] packetBefore = Enumerable.Range(0, scope.Packet.Capacity).Select(i => (byte)(i * 31)).ToArray();
        Marshal.Copy(packetBefore, 0, (IntPtr)scope.Packet.Memory, packetBefore.Length);
        scope.Packet.Seek(29);
        scope.Packet.Size = 71;

        if (shape == "nullRequest")
            Assert.Throws<ArgumentNullException>(() => trade.AddMoney(null!, scope.Packet));
        else if (shape == "nullPacket")
            Assert.Throws<ArgumentNullException>(() => trade.AddMoney(request, null!));
        else
            Assert.Throws<InvalidOperationException>(() => trade.AddMoney(request, scope.Packet));

        Assert.Equal(balance, player.Money);
        Assert.Equal(77L, peerPlayer.Money);
        Assert.Equal(31L, player.WHMoney);
        Assert.Equal(32L, peerPlayer.WHMoney);
        Assert.Equal(18u, player.targetTrade);
        Assert.Equal(17u, peerPlayer.targetTrade);
        Assert.Equal(escrow, trade.Money);
        Assert.Equal(19L, peerTrade.Money);
        Assert.Equal(14u, trade.ConquerPoints);
        Assert.Equal(15u, peerTrade.ConquerPoints);
        Assert.True(trade.Confirmed);
        Assert.True(peerTrade.Confirmed);
        Assert.True(trade.WindowOpen);
        Assert.Equal(peerWindow, peerTrade.WindowOpen);
        Assert.Same(ownerPlayer, owner.Player);
        Assert.Same(ownerInventory, owner.Inventory);
        Assert.Same(ownerMap, owner.Inventory?.ClientItems);
        Assert.Same(peerPlayer, target.Player);
        Assert.Same(peerInventory, target.Inventory);
        Assert.Same(peerMap, target.Inventory.ClientItems);
        Assert.Same(ownerTrade, owner.MyTrade);
        Assert.Same(peerTrade, target.MyTrade);
        Assert.Same(tradeTarget, trade.Target);
        Assert.Same(peerTarget, peerTrade.Target);
        Assert.Same(item, Assert.Single(trade.Items).Value);
        Assert.Same(peerItem, Assert.Single(peerTrade.Items).Value);
        Assert.Same(item, Assert.Single(owner.Inventory!.ClientItems).Value);
        Assert.Same(peerItem, Assert.Single(target.Inventory.ClientItems).Value);
        Assert.Equal(121u, item.Uid);
        Assert.Equal(122u, peerItem.Uid);
        Assert.Equal(7uL, request.Action);
        Assert.Equal(amount, request.Money);
        Assert.Equal(123uL, request.ItemUID);
        Assert.Equal(4u, request.CPS);
        Assert.Equal(5uL, request.Unknown);
        Assert.Equal(6uL, request.UID);
        var packetAfter = new byte[scope.Packet.Capacity];
        Marshal.Copy((IntPtr)scope.Packet.Memory, packetAfter, 0, packetAfter.Length);
        Assert.Equal(packetBefore, packetAfter);
        Assert.Equal(29, scope.Packet.Position);
        Assert.Equal(71, scope.Packet.Size);
        Assert.Null(owner.Socket);
        Assert.Null(target.Socket);
    }

}
