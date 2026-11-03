using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedVendorSuspiciousEligibilityTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public Task ActualOwnedGoldAndCpListingsRejectSuspiciousRootOrDepositedChild(bool cp, bool child)
    {
        var root = Item(ListedUid, SashType, suspicious: child ? (byte)0 : (byte)1,
            children: [Item(ChildUid, suspicious: child ? (byte)1 : (byte)0)]);
        return Run([root], [Item(BuyerKeptUid)], async f =>
        {
            await Start(f); var before = Before.Capture(f);
            Assert.False(f.Store.IsUnboundTradeItemOwnerCurrent(f.Connection(Fixture.Actor), f.Current()));

            await f.SendAsync(Fixture.Actor, Listing(cp).ToFrameBytes());

            before.AssertUnchanged(f); Assert.Empty(f.Current().LegacyVendorListings!.Listings);
            Assert.Contains("listing-item-not-tradable", f.Log.ToString());
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualSuspiciousChildDepositAfterValidListingRefusesPurchaseWithoutDebitingOrRemovingThatListing(bool cp) =>
        Run([Item(ListedUid, SashType), Item(ChildUid, suspicious: 1)], [Item(BuyerKeptUid)], async f =>
        {
            var booth = await Start(f); await List(f, cp);
            var listed = f.Current().LegacyVendorListings!;
            await f.SendAsync(Fixture.Actor, new LegacyWarehousePacket(ListedUid, LegacyWarehouseAction.ShashDepositItem,
                ChildUid, 0, []).ToFrameBytes());
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == ChildUid);
            var child = Assert.Single(Assert.Single(f.Current().PlayerItems.InventoryItems).DepositedItems);
            Assert.Equal(ChildUid, child.Uid); Assert.Equal((byte)1, child.Suspicious);
            AssertActors(f); Assert.Same(listed, f.Current().LegacyVendorListings);
            var before = Before.Capture(f);

            await f.SendAsync(Fixture.Peer, Purchase(booth).ToFrameBytes());

            before.AssertUnchanged(f); Assert.Same(listed, f.Current().LegacyVendorListings);
            Assert.True(listed.Contains(ListedUid)); Assert.Contains("seller-item-not-tradable", f.Log.ToString());
        });
}
