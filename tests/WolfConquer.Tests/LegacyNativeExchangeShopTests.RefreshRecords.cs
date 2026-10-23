using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    [InlineData("-1/")] [InlineData("bad/")] [InlineData("1")]
    [InlineData("1/7/101/")] [InlineData("1/7/101/2/extra/")]
    [InlineData("0/extra/")] [InlineData("0//")]
    [InlineData("1/7/101/-1/")] [InlineData("1/7/101/4294967296/")]
    [InlineData("1/-1/101/1/")] [InlineData("1/7/-1/1/")]
    [InlineData("4294967295/")]
    [InlineData("2/7/101/1/8/101/2/")]
    [InlineData("2/7/101/1/8/17/2/")]
    [InlineData("2/7/101/1/8/102/bad/")]
    public Task RefreshRecordMalformedTailNeverPublishesOrDefaultsAValidPrefix(string? text)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryLoad(text, out var reason)); Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
        Assert.Throws<FormatException>(() => scope.Shop.Load(text)); AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData("0")] [InlineData("0/")]
    public Task RefreshValidZeroRecordIsAnUnchangedNoMetadataOrRngOperation(string text)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        Assert.True(scope.Shop.TryLoad(text, out var reason), reason); AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RefreshRecordEngineeringBoundsRejectWithoutLargeCountPublication(bool textBound)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        string input = textBound ? new string('1', 1_048_577) :
            "4097/" + string.Concat(Enumerable.Range(1, 4097).Select(id => $"168/{1000 + id}/0/"));
        Assert.False(scope.Shop.TryLoad(input, out var reason)); Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshRecordMaterializationFaultPreservesRealPrefixRowsAndFrames()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var comparer = new RefreshThrowingComparer { ThrowOnCall = 1 };
        scope.Shop.Items = new Dictionary<uint, Shop.Item>(scope.Shop.Items, comparer);
        var before = CaptureRefresh(scope); comparer.Enabled = true;
        try
        {
            Assert.False(scope.Shop.TryLoad("1/168/1001/0/", out var reason));
            Assert.StartsWith("exchangeShop", reason); Assert.True(comparer.Calls > 0);
        }
        finally { comparer.Enabled = false; }
        AssertRefreshRejected(scope, before);
    });

    [Fact]
    public Task RefreshExactSavedRecordCountLimitRetainsAValidRoundTrip()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        string input = "4096/" + string.Concat(Enumerable.Range(1, 4096).Select(id => $"168/{1000 + id}/0/"));
        Assert.True(scope.Shop.TryLoad(input, out var reason), reason);
        Assert.Equal(4096, scope.Shop.Items.Count); Assert.Equal(input, scope.Shop.ToString());
        var fresh = new Shop(scope.Client);
        Assert.True(fresh.TryLoad(scope.Shop.ToString(), out reason), reason);
        Assert.Equal(input, fresh.ToString()); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(uint.MaxValue, uint.MaxValue, uint.MaxValue)]
    public Task RefreshSavedRecordPreservesEveryUnsignedBoundaryWithoutOpaqueIdentifierRestrictions(
        uint shop, uint goods, uint times)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        Assert.True(scope.Shop.TryLoad($"1/{shop}/{goods}/{times}/", out var reason), reason);
        Assert.NotSame(before.Items, scope.Shop.Items); ItemIs(scope.Shop.Items[goods], shop, goods, times);
        AssertDetachedRefreshStorageUnchanged(before); AssertRefreshSurroundingsUnchanged(scope, before);
    });
}
