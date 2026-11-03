using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlGoldBoothFileLeasesRetainTheSameListingThenAllowOnlyOnePurchase() =>
        AssertSqlVendorFileRefusalAsync(cp: false);

    [ModernSqlAuthFixtureFact]
    public Task RealSqlCpBoothFileLeasesRetainTheSameListingThenAllowOnlyOnePurchase() =>
        AssertSqlVendorFileRefusalAsync(cp: true);

    private static Task AssertSqlVendorFileRefusalAsync(bool cp) => Run(async f =>
    {
        var listing = await StartSqlVendorListingAsync(f, cp);
        foreach (var path in new[] { f.UserPath, f.ItemsPath })
        {
            var files = f.Data.Files(); var seller = f.Current; var buyer = f.Other;
            var book = Assert.IsType<LegacyVendorListingBook>(seller.LegacyVendorListings);
            Assert.True(book.TryGetListing(SqlWorldFixture.SoldUid, out var offered));
            var sellerImage = f.Native.OrdinarySpellBinding!.Items.Image;
            var buyerImage = f.Other.NativeGameClient!.OrdinarySpellBinding!.Items.Image;
            int writes = 0; f.Sent.Clear(); f.OtherSent.Clear();
            f.BeforeTradeWire = (_, _) => writes++;
            try
            {
                using var unavailable = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                await SendSqlVendorAsync(f, false, listing.Buy.ToFrameBytes());
                Assert.Equal(0, writes); Assert.Empty(f.Sent); Assert.Empty(f.OtherSent);
                Assert.Same(seller, f.Current); Assert.Same(buyer, f.Other);
                Assert.Same(book, f.Current.LegacyVendorListings);
                Assert.True(book.TryGetListing(SqlWorldFixture.SoldUid, out var retained)); Assert.Equal(offered, retained);
                Assert.Same(sellerImage, f.Native.OrdinarySpellBinding.Items.Image);
                Assert.Same(buyerImage, f.Other.NativeGameClient.OrdinarySpellBinding.Items.Image);
            }
            finally { f.BeforeTradeWire = null; }
            f.Data.AssertFiles(files); AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
            Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
        }
        await AssertSqlVendorPurchaseAsync(f, listing);
        await AssertSqlVendorReplayAsync(f, listing);
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Null(f.Current.LegacyVendorListings); AssertSqlVendorPaidState(f, listing);
        await AssertSqlVendorReplayAsync(f, listing);
        Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");
}
