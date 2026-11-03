namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeGoldBoothPublishesPaidUidTransferBeforeEverySocketThenColdRelogs() =>
        AssertSqlVendorRoundtripAsync(cp: false);

    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeCpBoothPublishesPaidUidTransferBeforeEverySocketThenColdRelogs() =>
        AssertSqlVendorRoundtripAsync(cp: true);

    private static Task AssertSqlVendorRoundtripAsync(bool cp) => Run(async f =>
    {
        var listing = await StartSqlVendorListingAsync(f, cp);
        await AssertSqlVendorPurchaseAsync(f, listing);
        await AssertSqlVendorReplayAsync(f, listing);
        var sellerItems = File.ReadAllBytes(f.ItemsPath); var buyerItems = File.ReadAllBytes(f.OtherItemsPath);
        var tickets = f.TicketCount;
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(tickets + 2, f.TicketCount);
        Assert.Null(f.Current.LegacyVendorListings); Assert.Null(f.Other.LegacyVendorListings);
        AssertSqlVendorPaidState(f, listing);
        Assert.Equal(sellerItems, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(buyerItems, File.ReadAllBytes(f.OtherItemsPath));
        await AssertSqlVendorReplayAsync(f, listing);
        Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");
}
