using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    private static void FillRefreshBook(PacketScope scope, int desiredCount)
    {
        for (uint id = 1001; scope.Shop.Items.Count < desiredCount; id++)
            scope.Shop.Items.Add(id, new() { shopid = 7, goods_rec_id = id, times = 0 });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RefreshFullBookAllowsExistingCounterUpdateButRefusesAnyNewRow(bool ex)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope); FillRefreshBook(scope, 4096);
        scope.State.Metadata(ex, 17, 17, 7, amount: 0);
        scope.State.Metadata(ex, 9999, 9999, 7, amount: 0);
        var before = CaptureRefresh(scope);
        Assert.True(ex ? scope.Shop.AddItemEx(7, 17, 2) : scope.Shop.AddItem(7, 17, 2));
        Assert.Equal(4096, scope.Shop.Items.Count); ItemIs(scope.Shop.Items[17], 7, 17, 13);
        AssertDetachedRefreshStorageUnchanged(before); AssertRefreshSurroundingsUnchanged(scope, before);
        var full = CaptureRefresh(scope);
        Assert.False(ex ? scope.Shop.AddItemEx(7, 9999, 0) : scope.Shop.AddItem(7, 9999, 0));
        AssertRefreshRejected(scope, full);
    });

    [Fact]
    public Task RefreshMergedLoadCanReachButCannotExceedTheSavedBookCountBound()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        string fit = "4094/" + string.Concat(Enumerable.Range(1, 4094).Select(id => $"7/{1000 + id}/0/"));
        Assert.True(scope.Shop.TryLoad(fit, out var reason), reason);
        Assert.Equal(4096, scope.Shop.Items.Count);
        AssertDetachedRefreshStorageUnchanged(before); AssertRefreshSurroundingsUnchanged(scope, before);
        var full = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryLoad("1/7/9999/0/", out reason));
        Assert.StartsWith("exchangeShop", reason); AssertRefreshRejected(scope, full);
    });

    [Fact]
    public Task RefreshRetainedPlusSelectedOverflowRefusesBeforeAnyRngDrawOrMutation()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope); FillRefreshBook(scope, 4096);
        ExCatalog(scope.State, 168, 10_001, 8);
        var random = new LegacyNativeSafeRandom(113);
        LegacyNativePoolRandom.GetRandom = random;
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryGetRandomEx(168, 8, out var reason));
        Assert.StartsWith("exchangeShop", reason); AssertRefreshRejected(scope, before);
        Assert.Equal(new LegacyNativeSafeRandom(113).Next(), random.Next());
    });

    [Fact]
    public Task RefreshFullBookCanReplaceRetiredSlotsWithoutCountingThemTwice()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope); FillRefreshBook(scope, 4088);
        ExCatalog(scope.State, 168, 10_001, 8);
        foreach (uint id in scope.State.ExRows.Keys)
            scope.Shop.Items.Add(id, new() { shopid = 168, goods_rec_id = id, times = 3 });
        var before = CaptureRefresh(scope);
        Assert.Equal(4096, scope.Shop.Items.Count);
        Assert.True(scope.Shop.TryGetRandomEx(168, 8, out var reason), reason);
        Assert.Equal(4096, scope.Shop.Items.Count);
        foreach (uint id in scope.State.ExRows.Keys) ItemIs(scope.Shop.Items[id], 168, id, 0);
        AssertDetachedRefreshStorageUnchanged(before); AssertRefreshSurroundingsUnchanged(scope, before);
    });

    [Fact]
    public Task RefreshRawOverCapBookCannotProduceAnUnloadableSaveOrBypassZeroAdmission()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope); FillRefreshBook(scope, 4097);
        var before = CaptureRefresh(scope);
        Assert.Throws<InvalidDataException>(() => scope.Shop.ToString());
        Assert.False(scope.Shop.TryLoad("0/", out var loadReason)); Assert.StartsWith("exchangeShop", loadReason);
        Assert.False(scope.Shop.TryGetRandomEx(168, 0, out var refreshReason)); Assert.StartsWith("exchangeShop", refreshReason);
        AssertRefreshRejected(scope, before);
    });
}
