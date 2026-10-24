using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Theory]
    [InlineData(0U, false)]
    [InlineData(1_100_003U, false)]
    [InlineData(1_100_006U, true)]
    [InlineData(1_100_009U, false)]
    public Task RegisteredBoothUnboundPlainOrSashPurchaseKeepsFullRowsBalancesFilesAndD13Sequence(
        uint sashItemId, bool cps) => RunBoothClosureAsync(async fixture =>
        {
            if (sashItemId == 0) fixture.Seed(BoothItem(10));
            else
            {
                fixture.Seed(BoothItem(10, sashItemId), BoothItem(11));
                await fixture.DepositAsync(11);
            }
            await fixture.StartAsync();
            var startFiles = fixture.ReadFiles();
            var listingFrameStart = fixture.Harness.Sent.Count;
            await fixture.ListAsync(cps);
            var listingResponse = Assert.Single(fixture.Harness.Sent.Skip(listingFrameStart));
            Assert.Equal("seller", listingResponse.Recipient);
            Assert.Equal(BoothFixture.ListingRequest(cps).ToFrameBytes(), listingResponse.Bytes);
            fixture.AssertFiles(startFiles); // Listing itself is not a payment or item-file change.
            var listing = Assert.Single(fixture.Seller.LegacyVendorListings!.Listings);
            Assert.Equal(10U, listing.ItemUid);
            Assert.Equal(125L, listing.Amount);
            Assert.Equal(cps ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, listing.CostMode);
            var before = fixture.Capture();
            var original = Assert.Single(before.Seller.PlayerItems.InventoryItems);

            await fixture.BuyAsync();

            Assert.Equal(cps ? 1_000L : 875L, fixture.Buyer.Character.Money);
            Assert.Equal(cps ? 75L : 200L, fixture.Buyer.Character.ConquerPoints);
            Assert.Equal(cps ? 300L : 425L, fixture.Seller.Character.Money);
            Assert.Equal(cps ? 175L : 50L, fixture.Seller.Character.ConquerPoints);
            Assert.Equal(new uint[] { 50, 10 }, fixture.Buyer.PlayerItems.InventoryItems.Select(item => item.Uid));
            Assert.Empty(fixture.Seller.PlayerItems.InventoryItems);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
            Assert.Single(before.Seller.LegacyVendorListings!.Listings); // Old immutable book is untouched.
            var expectedBuyerItems = before.Buyer.PlayerItems with
            {
                InventoryItems = before.Buyer.PlayerItems.InventoryItems.Append(original).ToArray(),
            };
            var expectedSellerItems = before.Seller.PlayerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() };
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Buyer.PlayerItems));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedSellerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Seller.PlayerItems));
            AssertBoothOtherStores(before.Buyer.PlayerItems, fixture.Buyer.PlayerItems);
            AssertBoothOtherStores(before.Seller.PlayerItems, fixture.Seller.PlayerItems);
            var expectedFiles = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))] = ProjectBoothWallet(
                    before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))],
                    fixture.Buyer.Character.Money, fixture.Buyer.Character.ConquerPoints),
                [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))] = ProjectBoothWallet(
                    before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))],
                    fixture.Seller.Character.Money, fixture.Seller.Character.ConquerPoints),
                [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Buyer))] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyerItems),
                [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Seller))] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedSellerItems),
            };
            fixture.AssertFiles(expectedFiles);
            var loader = new PlayerItemSnapshotLoader(fixture.Harness.PlayerItemsRoot);
            Assert.Equal(expectedFiles[Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Buyer))],
                PlayerItemSnapshotLoader.ToLegacyBytes(loader.LoadByCharacterUid(D13VendorTestHarness.BuyerCharacterUid)));
            Assert.Equal(expectedFiles[Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Seller))],
                PlayerItemSnapshotLoader.ToLegacyBytes(loader.LoadByCharacterUid(D13VendorTestHarness.SellerCharacterUid)));
            var frames = fixture.Harness.Sent.Skip(before.FrameCount).ToArray();
            Assert.Equal(6, frames.Length);
            var request = BoothFixture.BuyRequest();
            Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, frames.Select(frame => frame.Recipient));
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(original).ToFrameBytes(), frames[0].Bytes);
            Assert.Equal(request.ToFrameBytes(), frames[1].Bytes);
            Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2].Bytes);
            Assert.Equal(new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3].Bytes);
            var updateType = cps ? LegacyMsgUpdateDataType.ConquerPoints : LegacyMsgUpdateDataType.Money;
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.BuyerCharacterUid,
                [new(updateType, cps ? 75UL : 875UL)]).ToFrameBytes(), frames[4].Bytes);
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.SellerCharacterUid,
                [new(updateType, cps ? 175UL : 425UL)]).ToFrameBytes(), frames[5].Bytes);
            Assert.Contains("completed vendor purchase", fixture.Log.ToString()[before.LogLength..], StringComparison.Ordinal);
        });

    private static byte[] ProjectBoothWallet(byte[] before, long money, long cps)
    {
        var lines = Encoding.UTF8.GetString(before).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var suffix = lines[index].EndsWith('\r') ? "\r" : "";
            if (lines[index].StartsWith("Money=", StringComparison.Ordinal))
                lines[index] = "Money=" + money.ToString(CultureInfo.InvariantCulture) + suffix;
            else if (lines[index].StartsWith("ConquerPoints=", StringComparison.Ordinal))
                lines[index] = "ConquerPoints=" + cps.ToString(CultureInfo.InvariantCulture) + suffix;
        }
        return new UTF8Encoding(false).GetBytes(string.Join('\n', lines));
    }

    private static void AssertBoothOtherStores(PlayerItemSnapshot before, PlayerItemSnapshot after)
    {
        Assert.Same(before.RelicFuseItems, after.RelicFuseItems);
        Assert.Same(before.MythsoulBagItems, after.MythsoulBagItems);
        Assert.Same(before.EquipmentItems, after.EquipmentItems);
        Assert.Same(before.RuneItems, after.RuneItems);
        Assert.Same(before.WardrobeItems, after.WardrobeItems);
        Assert.Same(before.WarehouseItems, after.WarehouseItems);
    }
}
