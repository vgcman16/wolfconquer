using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Theory]
    [InlineData("book", "exchangeShopBookInvalid")]
    [InlineData("owner", "exchangeShopOwnerInvalid")]
    [InlineData("packet", "exchangeShopPacketUnavailable")]
    public Task QualityOpenRetainsBookThenOwnerThenPacketRefusalPrecedence(string first, string expectedReason)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var originalStorage = scope.Client.MyExchangeShop;
        if (first == "book") scope.Shop.Items.Add(101, null!);
        if (first != "packet") scope.Client.MyExchangeShop = null;
        var expectedStorage = scope.Client.MyExchangeShop;
        var before = CaptureRefresh(scope);
        try
        {
            Assert.False(scope.Shop.TryOpen(null, 77, 7, out var reason));
            Assert.Equal(expectedReason, reason);
            Assert.Same(expectedStorage, scope.Client.MyExchangeShop);
            AssertRefreshRejected(scope, before);
        }
        finally { scope.Client.MyExchangeShop = originalStorage; }
    });

    [Fact]
    public unsafe Task QualityOpenCapacityRefusalPrecedesTheRealCurrentBookComparer()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var comparer = new RefreshThrowingComparer { ThrowOnCall = 1 };
        scope.Shop.Items = new Dictionary<uint, Shop.Item>(scope.Shop.Items, comparer);
        using var small = new LegacyNativePacket(8);
        byte[] marker = Enumerable.Repeat((byte)0xB7, 8).ToArray();
        Marshal.Copy(marker, 0, (IntPtr)small.Memory, marker.Length);
        small.Size = 8; small.Seek(4);
        var before = CaptureRefresh(scope);
        comparer.Enabled = true;
        try
        {
            Assert.False(scope.Shop.TryOpen(small, 77, 7, out var reason));
            Assert.Equal("exchangeShopPacketTooLarge", reason);
            Assert.Equal(0, comparer.Calls);
            Assert.Equal(marker, NativeSpellTestFixture.Bytes(small));
            Assert.Equal(8, small.Size); Assert.Equal(4, small.Position);
        }
        finally { comparer.Enabled = false; }
        AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QualityRefreshInsufficientCatalogRefusesBeforeStagingOrDrawing(bool reset)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        ExCatalog(scope.State, 168, 1001, 1);
        var comparer = new RefreshThrowingComparer { ThrowOnCall = 1 };
        scope.Shop.Items = new Dictionary<uint, Shop.Item>(scope.Shop.Items, comparer);
        var random = new LegacyNativeSafeRandom(19101);
        LegacyNativePoolRandom.GetRandom = random;
        var before = CaptureRefresh(scope);
        comparer.Enabled = true;
        try
        {
            bool accepted = reset ? scope.Shop.TryReset(out var reason) : scope.Shop.TryGetRandomEx(168, 8, out reason);
            Assert.False(accepted);
            Assert.Equal("exchangeShopInsufficientCandidates", reason);
            Assert.Equal(0, comparer.Calls);
        }
        finally { comparer.Enabled = false; }
        AssertRefreshRejected(scope, before);
        Assert.Equal(new LegacyNativeSafeRandom(19101).Next(), random.Next());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QualityCounterMetadataBindingRefusesBeforeSameComparerMaterialization(bool ex)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        scope.State.Metadata(ex, 17, 17, 7, amount: 0);
        var comparer = new RefreshThrowingComparer { ThrowOnCall = 1 };
        scope.Shop.Items = new Dictionary<uint, Shop.Item>(scope.Shop.Items, comparer);
        var before = CaptureRefresh(scope);
        comparer.Enabled = true;
        try
        {
            Assert.False(ex ? scope.Shop.AddItemEx(8, 17, 0) : scope.Shop.AddItem(8, 17, 0));
            Assert.Equal(0, comparer.Calls);
        }
        finally { comparer.Enabled = false; }
        AssertRefreshRejected(scope, before);
    });

    [Theory]
    [InlineData("4097/7/101/bad/", "exchangeShopSavedRecordLimit")]
    [InlineData("2/7/101/0/", "exchangeShopSavedStateInvalid")]
    [InlineData("2/7/101/0/8/101/bad/", "exchangeShopSavedStateInvalid")]
    public Task QualitySavedHeaderAndRowFaultsRetainExactRefusalReasonsWithoutPublishingPrefixes(string text, string expectedReason)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); ReadyRefreshPrefix(scope);
        var before = CaptureRefresh(scope);
        Assert.False(scope.Shop.TryLoad(text, out var reason));
        Assert.Equal(expectedReason, reason);
        AssertRefreshRejected(scope, before);
    });
}
