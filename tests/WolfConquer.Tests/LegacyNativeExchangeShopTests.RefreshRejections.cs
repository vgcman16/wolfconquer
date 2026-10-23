using WolfConquer.Core.World;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData("missing", false)] [InlineData("missing", true)]
    [InlineData("empty", false)] [InlineData("empty", true)]
    [InlineData("short", false)] [InlineData("short", true)]
    [InlineData("foreign-only", false)] [InlineData("foreign-only", true)]
    [InlineData("null-tail", false)] [InlineData("null-tail", true)]
    [InlineData("foreign-null-tail", false)] [InlineData("foreign-null-tail", true)]
    [InlineData("wrong-key", false)] [InlineData("wrong-key", true)]
    [InlineData("same-row-alias", false)] [InlineData("same-row-alias", true)]
    public Task RefreshRejectsImpossibleOrMalformedCatalogWithoutPublishingAnyPrefix(string malformed, bool reset)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        var old = ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, malformed == "short" ? 7 : 9);
        switch (malformed)
        {
            case "missing": GoodsEx.exchange_shop_goods_ex = null!; break;
            case "empty": scope.State.ExRows.Clear(); break;
            case "foreign-only":
                foreach (var row in scope.State.ExRows.Values) row.shopid = 169;
                break;
            case "null-tail": scope.State.ExRows.Add(2001, null!); break;
            case "foreign-null-tail":
                scope.State.ExRows.Add(2001, new GoodsEx.item { id = 2001, shopid = 169 });
                scope.State.ExRows.Add(2002, null!);
                break;
            case "wrong-key": scope.State.ExRows[1009].id = 7009; break;
            case "same-row-alias": scope.State.ExRows.Add(2001, scope.State.ExRows[1001]); break;
        }
        var before = CaptureRefresh(scope, old);
        var random = new LegacyNativeSafeRandom(29);
        LegacyNativePoolRandom.GetRandom = random;
        before = before with { Random = random };
        var expectedRandom = new LegacyNativeSafeRandom(29);
        bool accepted = reset ? scope.Shop.TryReset(out var reason) : scope.Shop.TryGetRandomEx(168, 8, out reason);
        Assert.False(accepted);
        Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
        Assert.Equal(expectedRandom.Next(), random.Next()); // Validation did not spend a random draw.
    });

    [Theory]
    [InlineData("missing", false)] [InlineData("missing", true)]
    [InlineData("null-tail", false)] [InlineData("null-tail", true)]
    [InlineData("wrong-key", false)] [InlineData("wrong-key", true)]
    [InlineData("row-alias", false)] [InlineData("row-alias", true)]
    [InlineData("foreign-collision", false)] [InlineData("foreign-collision", true)]
    public Task RefreshRejectsMalformedBookBeforeRngOrAnyRemoval(string malformed, bool reset)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        var old = ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 8);
        switch (malformed)
        {
            case "missing": scope.Shop.Items = null!; break;
            case "null-tail": scope.Shop.Items.Add(101, null!); break;
            case "wrong-key": scope.Shop.Items[19].goods_rec_id = 777; break;
            case "row-alias": scope.Shop.Items.Add(101, old); break;
            case "foreign-collision": scope.Shop.Items.Add(1001,
                new() { shopid = 169, goods_rec_id = 1001, times = 17 }); break;
        }
        var random = new LegacyNativeSafeRandom(37);
        LegacyNativePoolRandom.GetRandom = random;
        var before = CaptureRefresh(scope, old);
        bool accepted = reset ? scope.Shop.TryReset(out var reason) : scope.Shop.TryGetRandomEx(168, 8, out reason);
        Assert.False(accepted);
        Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
        Assert.Equal(new LegacyNativeSafeRandom(37).Next(), random.Next());
    });

    [Fact]
    public Task RefreshRefusesUInt32MaxCountWithoutAllocatingRequestedCountOrRemovingOldRows()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 8);
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryGetRandomEx(168, uint.MaxValue, out var reason));
        Assert.StartsWith("exchangeShop", reason);
        AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData("select")] [InlineData("reset")] [InlineData("loading")]
    public Task RefreshVoidCompatibilityWrappersRefuseExplicitlyWithoutPrefixLoss(string operation)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        if (operation == "select") scope.Shop.GetRandomEx(168, 8);
        else if (operation == "reset") scope.Shop.Reset();
        else scope.Shop.Loading();
        AssertRefreshRejected(scope, before);
        Assert.Contains("exchangeShop", scope.State.ConsoleText.ToString());
    });

    [Fact]
    public Task RefreshMissingRequiredRngRefusesWithRealBookAndFramesUnchanged()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 8);
        var random = LegacyNativePoolRandom.GetRandom;
        LegacyNativePoolRandom.GetRandom = null!;
        try
        {
            var before = CaptureRefresh(scope);
            Assert.False(scope.Shop.TryGetRandomEx(168, 8, out var reason));
            Assert.StartsWith("exchangeShop", reason);
            AssertRefreshRejected(scope, before);
        }
        finally { LegacyNativePoolRandom.GetRandom = random; }
    });
}
