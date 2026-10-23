using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public void GetTimeCountsToTheNextLocalCalendarMidnightFromOneNowSample()
    {
        using var state = new State(); DateTime before = DateTime.Now;
        uint actual = state.Shop.GetTime(); DateTime after = DateTime.Now;
        TimeBetween(actual, before, after);
    }

    [Fact]
    public Task OpenSendsRequiredFieldsAndEmptyItemsThroughActualClientQueue() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); scope.Client.Player.UID = 123;
        DateTime before = DateTime.Now;
        Assert.True(scope.Shop.TryOpen(scope.Packet, 77, 7, out var reason), reason);
        DateTime after = DateTime.Now;
        scope.Sent(77, 7, before, after); Assert.Empty(scope.Shop.Items); Assert.Same(scope.Shop, scope.Client.MyExchangeShop);
    });

    [Fact]
    public Task OpenFiltersPhysicalShopAndEmitsPhysicalGoodsCountsInDictionaryOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); scope.Client.Player.UID = uint.MaxValue;
        var first = new Shop.Item { shopid = 7, goods_rec_id = 17, times = 0 };
        scope.Shop.Items[17] = first;
        scope.Shop.Items[19] = new Shop.Item { shopid = 8, goods_rec_id = 19, times = 9 };
        scope.Shop.Items[uint.MaxValue] = new Shop.Item { shopid = 7, goods_rec_id = uint.MaxValue, times = uint.MaxValue };
        var items = scope.Shop.Items.Values.Where(row => row.shopid == 7).ToArray();
        string record = scope.Shop.ToString(); DateTime before = DateTime.Now;
        Assert.True(scope.Shop.TryOpen(scope.Packet, uint.MaxValue, 7, out var reason), reason);
        DateTime after = DateTime.Now;
        scope.Sent(uint.MaxValue, 7, before, after, items.Select(row => (row.goods_rec_id, row.times)).ToArray());
        Assert.Equal(record, scope.Shop.ToString()); Assert.Empty(scope.State.GoodsRows); Assert.Empty(scope.State.ExRows);
        Assert.Same(first, scope.Shop.Items[17]);
    });

    [Theory]
    [InlineData("user")] [InlineData("items")] [InlineData("row")]
    [InlineData("stream")] [InlineData("player")]
    [InlineData("missing-storage")] [InlineData("foreign-storage")]
    public Task OpenInvalidOwnersDoNotRepairStateWriteCallerPacketOrQueueAnything(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); Shop shop = fault == "user" ? new Shop(null!) : scope.Shop;
        if (fault == "items") shop.Items = null!;
        else if (fault == "row") shop.Items[17] = null!;
        var player = scope.Client.Player;
        var storage = scope.Client.MyExchangeShop;
        if (fault == "player") scope.Client.Player = null!;
        else if (fault == "missing-storage") scope.Client.MyExchangeShop = null;
        else if (fault == "foreign-storage") scope.Client.MyExchangeShop = new Shop(scope.Client);
        try
        {
            Assert.False(shop.TryOpen(fault == "stream" ? null! : scope.Packet, 77, 7, out var reason));
            Assert.StartsWith("exchangeShop", reason); scope.Unwritten();
        }
        finally { scope.Client.Player = player; scope.Client.MyExchangeShop = storage; }
    });
}
