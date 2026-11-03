using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    private static PlayerItemSnapshotItem SuspiciousBoothItem(uint uid) => new()
    {
        Uid = uid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
        StackSize = 1, Suspicious = 1, OwnerUid = uid, OwnerName = "BoothOriginal",
        Signature = "complete row sentinel",
    };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredBoothRejectsSuspiciousSelectedRootOrDepositedChild(bool child) =>
        RunBoothClosureAsync(async fixture =>
        {
            if (child)
            {
                fixture.Seed(BoothItem(10, 1_100_009), SuspiciousBoothItem(11));
                await fixture.DepositAsync(11);
                Assert.Equal((byte)1, Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems).Suspicious);
            }
            else fixture.Seed(SuspiciousBoothItem(10));
            await fixture.StartAsync();
            var before = fixture.Capture();

            await fixture.ListAsync();

            fixture.AssertUnchanged(before);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
        });

    [Fact]
    public Task RegisteredBoothRechecksSuspiciousChildDepositedAfterListingBeforePurchase() =>
        RunBoothClosureAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), SuspiciousBoothItem(11));
            await fixture.StartAsync();
            await fixture.ListAsync();
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            await fixture.DepositAsync(11); // Actual registered sash transfer and file writer.
            var before = fixture.Capture();

            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            Assert.DoesNotContain(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
            var child = Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems);
            Assert.Equal(11U, child.Uid);
            Assert.Equal((byte)1, child.Suspicious);
        });

    [Fact]
    public Task RegisteredBoothSellsEligibleSelectionAndRetainsUnselectedSuspiciousItem() =>
        RunBoothClosureAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10), SuspiciousBoothItem(11));
            await fixture.StartAsync();
            await fixture.ListAsync();
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            var before = fixture.Capture();
            var selected = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 10);
            var retained = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 11);

            await fixture.BuyAsync();

            Assert.Equal(875L, fixture.Buyer.Character.Money);
            Assert.Equal(425L, fixture.Seller.Character.Money);
            Assert.Equal(200L, fixture.Buyer.Character.ConquerPoints);
            Assert.Equal(50L, fixture.Seller.Character.ConquerPoints);
            var buyerItems = before.Buyer.PlayerItems with
            { InventoryItems = before.Buyer.PlayerItems.InventoryItems.Append(selected).ToArray() };
            var sellerItems = before.Seller.PlayerItems with { InventoryItems = [retained] };
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(buyerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Buyer.PlayerItems));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(sellerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Seller.PlayerItems));
            Assert.Equal((byte)1, Assert.Single(fixture.Seller.PlayerItems.InventoryItems).Suspicious);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
            var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))] = ProjectBoothWallet(
                    before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))], 875, 200),
                [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))] = ProjectBoothWallet(
                    before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))], 425, 50),
                [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Buyer))] = PlayerItemSnapshotLoader.ToLegacyBytes(buyerItems),
                [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Seller))] = PlayerItemSnapshotLoader.ToLegacyBytes(sellerItems),
            };
            fixture.AssertFiles(files);
            var frames = fixture.Harness.Sent.Skip(before.FrameCount).ToArray();
            Assert.Equal(6, frames.Length);
            Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, frames.Select(frame => frame.Recipient));
            var request = BoothFixture.BuyRequest();
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(selected).ToFrameBytes(), frames[0].Bytes);
            Assert.Equal(request.ToFrameBytes(), frames[1].Bytes);
            Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2].Bytes);
            Assert.Equal(new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3].Bytes);
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.BuyerCharacterUid,
                [new(LegacyMsgUpdateDataType.Money, 875)]).ToFrameBytes(), frames[4].Bytes);
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.SellerCharacterUid,
                [new(LegacyMsgUpdateDataType.Money, 425)]).ToFrameBytes(), frames[5].Bytes);
        });
}
