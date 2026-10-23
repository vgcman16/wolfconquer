namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData(false, "overflow")] [InlineData(true, "overflow")]
    [InlineData(false, "wrong-shop")] [InlineData(true, "wrong-shop")]
    [InlineData(false, "catalog-key")] [InlineData(true, "catalog-key")]
    [InlineData(false, "catalog-null-tail")] [InlineData(true, "catalog-null-tail")]
    [InlineData(false, "book-null-tail")] [InlineData(true, "book-null-tail")]
    [InlineData(false, "book-alias")] [InlineData(true, "book-alias")]
    public Task RefreshAccountingRejectsInvalidWholeOwnersWithEveryEarlierFrameUnchanged(bool ex, string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); var old = ReadyRefreshPrefix(scope);
        scope.State.Metadata(ex, 17, 17, 7, 0);
        if (malformed == "overflow") old.times = uint.MaxValue;
        else if (malformed == "catalog-key") scope.State.Metadata(ex, 17, 777, 7, 0);
        else if (malformed == "catalog-null-tail")
        {
            if (ex) scope.State.ExRows.Add(101, null!);
            else scope.State.GoodsRows.Add(101, null!);
        }
        else if (malformed == "book-null-tail") scope.Shop.Items.Add(101, null!);
        else if (malformed == "book-alias") scope.Shop.Items.Add(101, old);
        var before = CaptureRefresh(scope);
        uint shop = malformed == "wrong-shop" ? 8u : 7u;
        bool accepted = ex ? scope.Shop.AddItemEx(shop, 17, 1) : scope.Shop.AddItem(shop, 17, 1);
        Assert.False(accepted); AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RefreshAccountingValidCowCommitPreservesEscapedRowsAndZeroAddIsIdempotent(bool ex)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); var old = ReadyRefreshPrefix(scope);
        scope.State.Metadata(ex, 17, 17, 7, 20);
        var before = CaptureRefresh(scope);
        Assert.True(ex ? scope.Shop.AddItemEx(7, 17, 2) : scope.Shop.AddItem(7, 17, 2));
        Assert.NotSame(before.Items, scope.Shop.Items);
        Assert.NotSame(old, scope.Shop.Items[17]); ItemIs(scope.Shop.Items[17], 7, 17, 13);
        Assert.Same(before.Entries![1].Value, scope.Shop.Items[19]);
        AssertDetachedRefreshStorageUnchanged(before); AssertRefreshSurroundingsUnchanged(scope, before);
        var committed = CaptureRefresh(scope);
        Assert.True(ex ? scope.Shop.AddItemEx(7, 17, 0) : scope.Shop.AddItem(7, 17, 0));
        AssertRefreshRejected(scope, committed); // Committed no-op, not a refusal.
    });
}
