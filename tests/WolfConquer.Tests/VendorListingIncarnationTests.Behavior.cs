using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class VendorListingIncarnationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RegisteredDetachedUnchangedListingStillTransfersWholeClosureAndExactPrice(bool cp)
    {
        using var f = new ComponentFixture(cp);
        await f.StartAndListAsync();
        var before = Before.Capture(f);
        await AssertCurrentShowAsync(f);
        f.RequireFiles(before.Files);
        await f.BuyAsync();
        AssertSale(f, before);
        f.Harness.Sent.Clear(); var settled = Before.Capture(f);
        Assert.Contains("seller-listing-missing", await f.BuyAsync(), StringComparison.Ordinal);
        AssertUnchanged(f, settled);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RegisteredDetachedExplicitRemovalAndRelistingCreatesANewIncarnationAtItsNewPrice(bool cp)
    {
        using var f = new ComponentFixture(cp);
        await f.StartAndListAsync(); f.ChangeCurrentItem("equal-root");
        var stale = Before.Capture(f);
        Assert.Contains("seller-listed-item-incarnation-changed", await f.BuyAsync(), StringComparison.Ordinal);
        AssertUnchanged(f, stale);

        await f.RemoveAsync();
        f.RequireFiles(stale.Files);
        await f.ListAsync(150);
        Assert.NotSame(stale.Book, f.Seller.LegacyVendorListings);
        var current = Before.Capture(f);
        Assert.Equal(150L, Assert.Single(current.Listings).Amount);
        await AssertCurrentShowAsync(f);
        await f.BuyAsync();
        AssertSale(f, current);
    }

    private static async Task AssertCurrentShowAsync(ComponentFixture f)
    {
        f.Harness.Sent.Clear(); await f.ShowAsync();
        var frame = Assert.Single(f.Harness.Sent); Assert.Equal("buyer", frame.Recipient);
        Assert.True(LegacyItemViewPacket.TryParse(D13VendorTestHarness.ReadFrame(frame.Bytes), out var item));
        Assert.Equal(ComponentFixture.SoldUid, item.Uid); Assert.Equal(f.Root.ItemId, item.ItemId);
        Assert.Equal(f.BoothUid, item.PlayerUid); Assert.Equal(f.Price, item.Cost);
        Assert.Equal((ushort)(f.Cp ? LegacyItemViewActionMode.CPs : LegacyItemViewActionMode.Gold), item.Mode);
        f.Harness.Sent.Clear();
    }

    private static void AssertSale(ComponentFixture f, Before before)
    {
        var buyerMoney = before.Buyer.Character.Money - (f.Cp ? 0 : f.Price);
        var sellerMoney = before.Seller.Character.Money + (f.Cp ? 0 : f.Price);
        var buyerCp = before.Buyer.Character.ConquerPoints - (f.Cp ? f.Price : 0);
        var sellerCp = before.Seller.Character.ConquerPoints + (f.Cp ? f.Price : 0);
        Assert.Equal((buyerMoney, buyerCp), (f.Buyer.Character.Money, f.Buyer.Character.ConquerPoints));
        Assert.Equal((sellerMoney, sellerCp), (f.Seller.Character.Money, f.Seller.Character.ConquerPoints));
        Assert.Equal((buyerMoney, buyerCp), f.Harness.ReadBalances(f.Buyer.AccountId));
        Assert.Equal((sellerMoney, sellerCp), f.Harness.ReadBalances(f.Seller.AccountId));
        Assert.Equal(before.Buyer.Character.Money + before.Seller.Character.Money, buyerMoney + sellerMoney);
        Assert.Equal(before.Buyer.Character.ConquerPoints + before.Seller.Character.ConquerPoints, buyerCp + sellerCp);
        Assert.Empty(f.Seller.PlayerItems.InventoryItems); Assert.Empty(f.Seller.LegacyVendorListings!.Listings);
        var received = Assert.Single(f.Buyer.PlayerItems.InventoryItems);
        Assert.NotSame(f.Root, received); Assert.Equal(ComponentFixture.SoldUid, received.Uid);
        Assert.Equal(before.SellerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Buyer.PlayerItems));
        Assert.Equal(before.SellerItems, File.ReadAllBytes(f.BuyerItemsPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), File.ReadAllBytes(f.SellerItemsPath));
        Assert.Equal(f.Root.DepositedItems.Select(child => child.Uid), received.DepositedItems.Select(child => child.Uid));
        Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, f.Harness.Sent.Select(sent => sent.Recipient));
        var frames = f.Harness.Sent.Select(sent => sent.Bytes).ToArray();
        Assert.Equal(6, frames.Length);
        Assert.True(LegacyGameItemPacket.TryParse(D13VendorTestHarness.ReadFrame(frames[0]), out var added));
        Assert.Equal(ComponentFixture.SoldUid, added.Uid); Assert.Equal(f.Root.ItemId, added.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, added.Mode);
        Assert.Equal(f.BuyRequest().ToFrameBytes(), frames[1]);
        Assert.Equal((f.BuyRequest() with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2]);
        Assert.Equal(new MsgItemUsagePacket(ComponentFixture.SoldUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3]);
        AssertWallet(frames[4], D13VendorTestHarness.BuyerCharacterUid, f.Cp, f.Cp ? buyerCp : buyerMoney);
        AssertWallet(frames[5], D13VendorTestHarness.SellerCharacterUid, f.Cp, f.Cp ? sellerCp : sellerMoney);
        f.RequireDetachedCurrentOwners(); Assert.False(LegacyTradeJournal.HasPending(f.Harness.UsersRoot));
    }

    private static void AssertWallet(byte[] bytes, uint uid, bool cp, long value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(D13VendorTestHarness.ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        Assert.Equal(new LegacyMsgUpdateEntry(cp ? LegacyMsgUpdateDataType.ConquerPoints : LegacyMsgUpdateDataType.Money,
            checked((ulong)value)), Assert.Single(update.Updates));
    }
}
