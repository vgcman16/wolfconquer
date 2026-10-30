using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupVendorRecoveryTests
{
    [Theory]
    [InlineData(false, "funds")] [InlineData(true, "funds")]
    [InlineData(false, "missing-listing")] [InlineData(true, "missing-listing")]
    [InlineData(false, "foreign-booth")]
    public Task ANewPurchaseStillNeedsActualListingFundsAndVisibleOwnerAfterRecoveryWorkExists(bool cp, string fault) =>
        WithFixture(f => f.AssertLiveBuyRefusedAsync(fault), cp);

    [Fact]
    public Task ANewPurchaseCannotUseRecordedSettlementRulesToDeliverIntoAFullFortyRootInventory() =>
        WithFixture(f => f.AssertLiveBuyRefusedAsync("capacity"), buyerCount: 40);
}

internal sealed partial class LegacyOwnedStartupVendorRecoveryFixture
{
    internal async Task AssertLiveBuyRefusedAsync(string fault)
    {
        var request = BuyRequest();
        if (fault is "funds" or "missing-listing")
        {
            await SendAsync(SellerUid, new MsgItemUsagePacket(SoldUid, 0, MsgItemUsageAction.RemoveVendingItem, 123, 0, 0, 0).ToFrameBytes());
            Assert.Empty(Current(SellerUid).LegacyVendorListings!.Listings);
            if (fault == "funds")
            {
                var amount = IsCp ? 61UL : 2_001UL;
                var action = IsCp ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold;
                await SendAsync(SellerUid, new MsgItemUsagePacket(SoldUid, amount, action, 124, 0, 0, 0).ToFrameBytes());
                Assert.True(Current(SellerUid).LegacyVendorListings!.TryGetListing(SoldUid, out var listing));
                Assert.Equal(checked((long)amount), listing.Amount);
            }
        }
        else if (fault == "foreign-booth") request = request with { DwParam = BoothUid + 1 };
        var files = Data.Files(); var buyer = Current(BuyerUid); var seller = Current(SellerUid);
        int firstBuyer = frames[BuyerUid].Count, firstSeller = frames[SellerUid].Count;
        await SendAsync(BuyerUid, request.ToFrameBytes());
        Assert.Same(buyer, Current(BuyerUid)); Assert.Same(seller, Current(SellerUid));
        Assert.Equal(firstBuyer, frames[BuyerUid].Count); Assert.Equal(firstSeller, frames[SellerUid].Count);
        Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        var expected = fault switch
        {
            "funds" => "purchase-plan-rejected:InsufficientBuyerBalance",
            "capacity" => "purchase-plan-rejected:BuyerInventoryFull",
            "missing-listing" => "seller-listing-missing",
            "foreign-booth" => "seller-vendor-session-not-found",
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        Assert.Contains(expected, Log.ToString(), StringComparison.Ordinal);
    }
}
