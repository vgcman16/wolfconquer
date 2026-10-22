using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public void GetTimeFollowsNativeThreeNowOffsetArithmeticRatherThanCorrectingToMidnight()
    {
        using var state = new State();
        DateTime before = DateTime.Now;
        uint actual = state.Shop.GetTime(); // Null player is deliberately irrelevant.
        DateTime after = DateTime.Now;
        TimeBetween(actual, before, after);
    }

    [Fact]
    public Task OpenSendsRequiredFieldsAndEmptyItemsThroughActualClientQueue() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        scope.Client.Player.UID = 123;
        DateTime before = DateTime.Now;
        scope.Shop.Open(scope.Packet, 77, 7);
        DateTime after = DateTime.Now;
        scope.Sent(77, 7, before, after);
        Assert.Empty(scope.Shop.Items);
        Assert.Null(scope.Client.MyExchangeShop);
    });

    [Fact]
    public Task OpenFiltersPhysicalShopAndEmitsPhysicalGoodsCountsInDictionaryEnumerationOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        scope.Client.Player.UID = uint.MaxValue;
        var first = new Shop.Item { shopid = 7, goods_rec_id = 17, times = 0 };
        scope.Shop.Items[100] = first;
        scope.Shop.Items[1] = new Shop.Item { shopid = 8, goods_rec_id = 19, times = 9 };
        scope.Shop.Items[2] = new Shop.Item { shopid = 7, goods_rec_id = uint.MaxValue, times = uint.MaxValue };
        var items = scope.Shop.Items.Values.Where(row => row.shopid == 7).ToArray();
        string record = scope.Shop.ToString();
        DateTime before = DateTime.Now;
        scope.Shop.Open(scope.Packet, uint.MaxValue, 7);
        DateTime after = DateTime.Now;
        scope.Sent(uint.MaxValue, 7, before, after,
            items.Select(row => (row.goods_rec_id, row.times)).ToArray());
        Assert.Equal(record, scope.Shop.ToString());
        Assert.Empty(scope.State.GoodsRows); // Open does not need either metadata catalog.
        Assert.Empty(scope.State.ExRows);
        Assert.Same(first, scope.Shop.Items[100]);
    });

    [Theory]
    [InlineData("user")]
    [InlineData("items")]
    [InlineData("row")]
    [InlineData("stream")]
    public Task OpenPhysicalFaultsDoNotRepairOwnersWriteCallerPacketOrQueueAnything(string fault) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            Shop shop = fault == "user" ? new Shop(null!) : scope.Shop;
            if (fault == "items") shop.Items = null!;
            else if (fault == "row") shop.Items[17] = null!;
            Assert.Throws<NullReferenceException>(() => shop.Open(fault == "stream" ? null! : scope.Packet, 77, 7));
            scope.Unwritten();
            Assert.Null(scope.Client.MyExchangeShop);
        });
}
