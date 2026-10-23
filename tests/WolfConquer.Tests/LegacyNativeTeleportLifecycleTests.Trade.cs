using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task TradeBirthAndClientPredicateUseTheRealWindowFlag()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var trade = new LegacyNativeTradeRuntime(scope.Client);
            Assert.Same(scope.Client, trade.Owner); Assert.Null(trade.Target); Assert.Empty(trade.Items);
            Assert.Equal(0u, trade.ConquerPoints); Assert.Equal(0L, trade.Money);
            Assert.False(trade.Confirmed); Assert.False(trade.WindowOpen);
            Assert.Null(scope.Client.MyTrade); Assert.False(scope.Client.InTrade);
            scope.Client.MyTrade = trade; Assert.False(scope.Client.InTrade);
            trade.WindowOpen = true; Assert.True(scope.Client.InTrade);
            var nullOwner = new LegacyNativeTradeRuntime(null!);
            Assert.Null(nullOwner.Owner); Assert.NotNull(nullOwner.Items);
            Assert.Equal(0u, scope.Client.Player.targetTrade);
        });
    }

    [Fact]
    public Task TradeItemInTradeUsesItemIdRatherThanUid()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var trade = new LegacyNativeTradeRuntime(scope.Client);
            var item = Item(100, 410000); trade.Items.TryAdd(100, item);
            Assert.False(trade.ItemInTrade(item));
            trade.Items.TryAdd(410000, Item(200));
            Assert.True(trade.ItemInTrade(item));
            Assert.Throws<NullReferenceException>(() => trade.ItemInTrade(null!));
        });
    }

    [Fact]
    public Task TradeValidityUsesValueUidAgainstRealInventoryNotEscrowKeyOrObjectIdentity()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var trade = new LegacyNativeTradeRuntime(scope.Client);
            Assert.True(trade.ValidItems()); // Native empty enumeration does not dereference Inventory.
            var item = Item(12); trade.Items.TryAdd(99, item);
            Assert.Throws<NullReferenceException>(() => trade.ValidItems());
            scope.Client.Inventory = new LegacyNativeInventoryRuntime(scope.Client);
            Assert.False(trade.ValidItems());
            scope.Client.Inventory.ClientItems.TryAdd(12, Item(12, 420000));
            Assert.True(trade.ValidItems());
            item.Uid = 13; Assert.False(trade.ValidItems());
        });
    }

    [Theory]
    [InlineData(1000L, 125L, 875L, 125L)]
    [InlineData(1000L, 0L, 1000L, 0L)]
    [InlineData(1000L, -125L, 1125L, -125L)]
    public Task TradeMoneyEscrowRunsRealWalletSetterThenTargetProto(long balance, long amount, long after, long escrow)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var targetQueue = new Queue<byte[]>(); var target = scope.Peer(18, targetQueue);
            var trade = new LegacyNativeTradeRuntime(scope.Client) { Target = target, WindowOpen = true };
            scope.Client.MyTrade = trade; target.MyTrade = new LegacyNativeTradeRuntime(target) { WindowOpen = true, Target = scope.Client };
            scope.Client.Player.Money = balance; scope.Queue.Clear();
            var request = new LegacyNativeMsgTrade.Trade { Action = 7, Money = amount, ItemUID = 123, CPS = 4, Unknown = 5, UID = 6 };
            trade.AddMoney(request, scope.Packet);
            Assert.Equal(after, scope.Client.Player.Money); Assert.Equal(escrow, trade.Money);
            Assert.Equal(8uL, request.Action);
            Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
            Assert.Equal(TradeBytes(8, amount, 123, 4, 5, 6), Assert.Single(targetQueue));
            Assert.Same(trade, scope.Client.MyTrade);
        });
    }

    [Fact]
    public Task TradeClosedTargetShortCircuitsBeforeNullRequestAndOwner()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var target = scope.Peer(18, new());
            var trade = new LegacyNativeTradeRuntime(null!) { Target = target };
            trade.AddMoney(null!, null!);
            Assert.Equal(0L, trade.Money);
            trade.Target = null;
            Assert.Throws<NullReferenceException>(() => trade.AddMoney(null!, null!));
        });
    }

    [Fact]
    public Task TradeInsufficientFundsPreservesRequestAndBothQueues()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var queue = new Queue<byte[]>(); var target = scope.Peer(18, queue);
            target.MyTrade = new LegacyNativeTradeRuntime(target) { WindowOpen = true };
            var trade = new LegacyNativeTradeRuntime(scope.Client) { Target = target, Money = 9 };
            scope.Client.Player.Money = 5; scope.Queue.Clear();
            var request = new LegacyNativeMsgTrade.Trade { Action = 7, Money = 6 };
            trade.AddMoney(request, null!);
            Assert.Equal(5, scope.Client.Player.Money); Assert.Equal(9, trade.Money);
            Assert.Equal(7uL, request.Action); Assert.Empty(scope.Queue); Assert.Empty(queue);
        });
    }

    [Fact]
    public Task TradeFaultAtFinalPacketKeepsAlreadyDebitedWalletAndEscrow()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var target = scope.Peer(18, new());
            target.MyTrade = new LegacyNativeTradeRuntime(target) { WindowOpen = true };
            var trade = new LegacyNativeTradeRuntime(scope.Client) { Target = target };
            scope.Client.Player.Money = 100; scope.Queue.Clear();
            var request = new LegacyNativeMsgTrade.Trade { Action = 7, Money = 25 };
            Assert.Throws<NullReferenceException>(() => trade.AddMoney(request, null!));
            Assert.Equal(75, scope.Client.Player.Money); Assert.Equal(25, trade.Money);
            Assert.Equal(8uL, request.Action); Assert.Single(scope.Queue);
        });
    }
}
