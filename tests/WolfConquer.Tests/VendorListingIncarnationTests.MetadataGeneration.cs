using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class VendorListingIncarnationTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task RegisteredIssuedListingCannotBorrowADifferentEmptyMetadataGeneration(bool cp, bool startEmpty)
    {
        using var f = new ComponentFixture(cp);
        var original = startEmpty ? NativeCallerWorldDataFixture.Create([], []) : null;
        f.Catalog = original;
        await f.StartAndListAsync();
        await AssertCurrentShowAsync(f);
        var before = Before.Capture(f);
        var sellerState = JsonSerializer.Serialize(f.Seller.Character);
        var buyerState = JsonSerializer.Serialize(f.Buyer.Character);
        var replacement = NativeCallerWorldDataFixture.Create([], []);
        var replacementOwner = Assert.IsType<LegacyTradeItemMetadata>(replacement.TradeItemMetadata);
        Assert.True(replacementOwner.HasEmptySource);
        Assert.True(replacementOwner.IsCurrent(replacement));
        if (original is not null)
        {
            Assert.NotSame(original, replacement);
            var originalOwner = Assert.IsType<LegacyTradeItemMetadata>(original.TradeItemMetadata);
            Assert.NotSame(originalOwner, replacementOwner);
            Assert.True(originalOwner.HasEmptySource);
        }

        f.Catalog = replacement;
        Assert.Contains("seller-listed-item-incarnation-changed", await f.ShowAsync(), StringComparison.Ordinal);
        AssertUnchanged(f, before);
        Assert.Equal(sellerState, JsonSerializer.Serialize(f.Seller.Character));
        Assert.Equal(buyerState, JsonSerializer.Serialize(f.Buyer.Character));
        Assert.Contains("seller-listed-item-incarnation-changed", await f.BuyAsync(), StringComparison.Ordinal);
        AssertUnchanged(f, before);
        Assert.Equal(sellerState, JsonSerializer.Serialize(f.Seller.Character));
        Assert.Equal(buyerState, JsonSerializer.Serialize(f.Buyer.Character));

        // Neither refusal consumes nor renews the original actual issuer's authority.
        f.Catalog = original;
        await AssertCurrentShowAsync(f);
        AssertUnchanged(f, before);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RegisteredIssuedListingWithUnchangedEmptyMetadataStillShowsAndSells(bool cp)
    {
        using var f = new ComponentFixture(cp);
        var catalog = NativeCallerWorldDataFixture.Create([], []);
        f.Catalog = catalog;
        var metadata = Assert.IsType<LegacyTradeItemMetadata>(catalog.TradeItemMetadata);
        Assert.True(metadata.HasEmptySource); Assert.True(metadata.IsCurrent(catalog));
        await f.StartAndListAsync();
        var before = Before.Capture(f);
        await AssertCurrentShowAsync(f);
        AssertUnchanged(f, before);
        Assert.Same(metadata, f.Catalog!.TradeItemMetadata);
        await f.BuyAsync();
        AssertSale(f, before);
        Assert.Same(metadata, f.Catalog!.TradeItemMetadata);
        f.Harness.Sent.Clear();
        var settled = Before.Capture(f);
        Assert.Contains("seller-listing-missing", await f.BuyAsync(), StringComparison.Ordinal);
        AssertUnchanged(f, settled);
    }

    private sealed partial class ComponentFixture
    {
        private readonly LegacyTradeStateStore catalogTrades = new();
        private readonly WorldOperationBarrier catalogOperations = new();
        private LegacyTradeRecoveryState? catalogRecovery;
        internal WorldDataCatalog? Catalog { get; set; }

        private async Task<string> DispatchCatalogAsync(bool buyer, MsgItemUsagePacket packet)
        {
            using var log = new StringWriter();
            catalogRecovery ??= new(Harness.UsersRoot, Harness.PlayerItemsRoot);
            Assert.True(catalogRecovery.TryEnsureInitialized(out var reason), reason);
            var context = new WorldPacketHandlerContext(buyer ? BuyerConnection : SellerConnection,
                log, buyer ? Buyer : Seller, WorldData: Catalog, Sessions: Harness.Sessions,
                PlayerItemsRoot: Harness.PlayerItemsRoot, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            {
                Trades = catalogTrades,
                TradeOperations = catalogOperations,
                TradeRecovery = catalogRecovery,
            };
            using var operation = packet.Action == MsgItemUsageAction.BuyVendingItem
                ? null : await catalogOperations.EnterSharedAsync();
            await Registry.HandleAsync(context, D13VendorTestHarness.ReadFrame(packet.ToFrameBytes()));
            return log.ToString();
        }
    }
}
