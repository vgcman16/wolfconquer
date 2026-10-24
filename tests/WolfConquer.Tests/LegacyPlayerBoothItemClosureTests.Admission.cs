using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Theory]
    [InlineData(false, "bound1")]
    [InlineData(false, "bound2")]
    [InlineData(false, "locked")]
    [InlineData(false, "inscribed")]
    [InlineData(false, "unable")]
    [InlineData(true, "bound1")]
    [InlineData(true, "bound2")]
    [InlineData(true, "locked")]
    [InlineData(true, "inscribed")]
    [InlineData(true, "unable")]
    public Task RegisteredBoothRejectsRestrictedRootOrDepositedChildWithoutPublishingListing(
        bool childRestricted, string restriction) => RunBoothClosureAsync(async fixture =>
        {
            if (childRestricted)
            {
                fixture.Seed(BoothItem(10, 1_100_009), RestrictedBoothItem(11, restriction));
                await fixture.DepositAsync(11);
            }
            else fixture.Seed(RestrictedBoothItem(10, restriction));
            await fixture.StartAsync(); // Actual registered StartVendor, never ActivateSeller.
            var before = fixture.Capture();

            await fixture.ListAsync();

            fixture.AssertUnchanged(before);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
        });

    [Theory]
    [InlineData("bound1")]
    [InlineData("bound2")]
    [InlineData("locked")]
    [InlineData("inscribed")]
    [InlineData("unable")]
    public Task RegisteredBoothRechecksActualDepositedChildAddedAfterValidListingBeforePurchase(string restriction) =>
        RunBoothClosureAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), RestrictedBoothItem(11, restriction));
            await fixture.StartAsync();
            // This unrelated restricted inventory item must not ban listing an
            // otherwise eligible empty sash. Only the selected closure is sold.
            await fixture.ListAsync();
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            await fixture.DepositAsync(11); // Genuine registered sash writer, after listing.
            var before = fixture.Capture();

            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            Assert.DoesNotContain(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
            var child = Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems);
            Assert.Equal(11U, child.Uid);
            Assert.Equal(RestrictedBoothItem(11, restriction).Bound, child.Bound);
            Assert.Equal(RestrictedBoothItem(11, restriction).Locked, child.Locked);
            Assert.Equal(RestrictedBoothItem(11, restriction).Inscribed, child.Inscribed);
            Assert.Equal(RestrictedBoothItem(11, restriction).ItemId, child.ItemId);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredBoothIncomingChildUidCannotCollideWithBuyerWarehouseRootOrDeposit(bool collideWithChild) =>
        RunBoothClosureAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), BoothItem(11));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            await fixture.ListAsync();
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            var items = fixture.Buyer.PlayerItems;
            var warehouse = collideWithChild
                ? new[] { Assert.Single(items.WarehouseItems).WithItemId(1_100_006)
                    .WithDepositedItems(new[] { BoothItem(11) }) }
                : items.WarehouseItems.Concat(new[] { BoothItem(11).WithWarehouseId(1000) })
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray();
            fixture.SaveBuyerItems(items with { WarehouseItems = warehouse });
            var before = fixture.Capture();

            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            Assert.DoesNotContain(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredBoothMalformedUnrelatedStoreTailCannotBecomeListingOrPurchaseAuthority(bool buyPhase) =>
        RunBoothClosureAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), BoothItem(11));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            if (buyPhase)
            {
                await fixture.ListAsync();
                Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            }
            var connection = buyPhase ? fixture.BuyerConnection : fixture.SellerConnection;
            var current = fixture.Harness.StoredSession(connection);
            fixture.Harness.Sessions.Set(connection, current with
            {
                PlayerItems = current.PlayerItems with
                {
                    WarehouseItems = current.PlayerItems.WarehouseItems.Concat(
                        new PlayerItemSnapshotItem[] { null! }).ToArray(),
                },
            });
            var before = fixture.Capture();

            if (buyPhase) await fixture.BuyAsync();
            else await fixture.ListAsync();

            fixture.AssertUnchanged(before);
            Assert.Equal(buyPhase, fixture.Seller.LegacyVendorListings!.Contains(10));
        });
}
