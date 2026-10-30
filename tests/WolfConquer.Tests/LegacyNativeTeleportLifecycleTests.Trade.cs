using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task TradeBirthRequiresAnOwnerAndClientPredicateUsesTheRealWindowFlag()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<ArgumentNullException>(() => new LegacyNativeTradeRuntime(null!));
            var trade = new LegacyNativeTradeRuntime(scope.Client);
            Assert.Same(scope.Client, trade.Owner);
            Assert.Null(trade.Target);
            Assert.Empty(trade.Items);
            Assert.Equal(0u, trade.ConquerPoints);
            Assert.Equal(0L, trade.Money);
            Assert.False(trade.Confirmed);
            Assert.False(trade.WindowOpen);
            Assert.Null(scope.Client.Inventory);
            Assert.Null(scope.Client.MyTrade);
            Assert.False(scope.Client.InTrade);
            scope.Client.MyTrade = trade;
            Assert.False(scope.Client.InTrade);
            trade.WindowOpen = true;
            Assert.True(scope.Client.InTrade);
            Assert.Equal(0u, scope.Client.Player.targetTrade);
            Assert.Empty(scope.Queue);
        });
    }
}
