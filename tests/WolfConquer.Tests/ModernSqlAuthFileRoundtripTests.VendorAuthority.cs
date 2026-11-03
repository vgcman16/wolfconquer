using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public async Task RealSqlNativeBoothKeepsSameNodeAuthorityAcrossUnrelatedWarehousePublication()
    {
        foreach (bool cp in new[] { false, true })
            await Run(async f =>
            {
                var listing = await StartSqlVendorListingAsync(f, cp);
                var row = ListedSqlVendorRow(f);
                var original = SqlVendorRowBytes(row);
                var node = ListedSqlVendorNode(f);
                var book = f.Current.LegacyVendorListings;
                var metadata = Assert.IsType<LegacyTradeItemMetadata>(f.VendorAuthorityCatalog.TradeItemMetadata);
                Assert.True(metadata.IsCurrent(f.VendorAuthorityCatalog));
                Assert.True(metadata.Allows(row));

                await MoveSqlVendorItemAsync(f, SqlWorldFixture.KeptUid, LegacyWarehouseAction.DepositItem);
                await MoveSqlVendorItemAsync(f, SqlWorldFixture.KeptUid, LegacyWarehouseAction.WithdrawItem);

                var current = ListedSqlVendorRow(f);
                Assert.NotSame(row, current);
                Assert.Equal(original, SqlVendorRowBytes(current));
                Assert.Same(node, ListedSqlVendorNode(f));
                Assert.Same(node.Item, f.Native.Inventory!.ClientItems[SqlWorldFixture.SoldUid]);
                Assert.Same(book, f.Current.LegacyVendorListings);
                Assert.Same(metadata, f.VendorAuthorityCatalog.TradeItemMetadata);
                Assert.True(metadata.IsCurrent(f.VendorAuthorityCatalog)); Assert.True(metadata.Allows(current));
                AssertSqlVendorListing(f, listing);
                Assert.Equal(listing.Before.Registry, File.ReadAllBytes(f.Data.State));
                await AssertSqlVendorAuthorityShownAsync(f, listing);

                // Only the expected saved order changes: the real old book is retained.
                listing = listing with { Before = SqlTradeBefore.Capture(f) };
                await AssertSqlVendorPurchaseAsync(f, listing);
                await AssertSqlVendorAuthorityColdReplayAsync(f, listing);
            }, tradeItemState: "valid");
    }

    [ModernSqlAuthFixtureFact]
    public async Task RealSqlNativeBoothRejectsReplacementNodeUntilRegisteredRemoveAndRelist()
    {
        foreach (bool cp in new[] { false, true })
            await Run(async f =>
            {
                var listing = await StartSqlVendorListingAsync(f, cp);
                var row = ListedSqlVendorRow(f);
                var original = SqlVendorRowBytes(row);
                var node = ListedSqlVendorNode(f);
                var book = f.Current.LegacyVendorListings;
                var metadata = Assert.IsType<LegacyTradeItemMetadata>(f.VendorAuthorityCatalog.TradeItemMetadata);

                await MoveSqlVendorItemAsync(f, SqlWorldFixture.SoldUid, LegacyWarehouseAction.DepositItem);
                await MoveSqlVendorItemAsync(f, SqlWorldFixture.SoldUid, LegacyWarehouseAction.WithdrawItem);

                var returned = ListedSqlVendorRow(f);
                Assert.NotSame(row, returned);
                Assert.Equal(original, SqlVendorRowBytes(returned));
                var replacement = ListedSqlVendorNode(f);
                Assert.NotSame(node, replacement); Assert.NotSame(node.Item, replacement.Item);
                Assert.Same(book, f.Current.LegacyVendorListings);
                Assert.Same(metadata, f.VendorAuthorityCatalog.TradeItemMetadata);
                Assert.True(metadata.IsCurrent(f.VendorAuthorityCatalog)); Assert.True(metadata.Allows(returned));
                Assert.Equal(listing.Before.Registry, File.ReadAllBytes(f.Data.State));
                AssertSqlVendorListing(f, listing);
                await AssertSqlVendorAuthorityStaleAsync(f, listing);

                listing = await RelistSqlVendorAuthorityAsync(f, listing);
                Assert.Same(replacement, ListedSqlVendorNode(f));
                await AssertSqlVendorAuthorityShownAsync(f, listing);
                await AssertSqlVendorPurchaseAsync(f, listing);
                await AssertSqlVendorAuthorityColdReplayAsync(f, listing);
            }, tradeItemState: "valid");
    }

    private static PlayerItemSnapshotItem ListedSqlVendorRow(SqlWorldFixture f) =>
        Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);

    private static LegacyOwnedNativeItemImage.Node ListedSqlVendorNode(SqlWorldFixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.True(binding.Items.Image.TryGetCanonicalInventoryNode(ListedSqlVendorRow(f), out var node));
        Assert.Same(node.Item, f.Native.Inventory!.ClientItems[SqlWorldFixture.SoldUid]);
        return node;
    }

    private static byte[] SqlVendorRowBytes(PlayerItemSnapshotItem row) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [row] });

    private static async Task MoveSqlVendorItemAsync(SqlWorldFixture f, uint uid, LegacyWarehouseAction action)
    {
        await SendSqlVendorAsync(f, true, new LegacyWarehousePacket(8, action, uid, 0, []).ToFrameBytes());
        if (action == LegacyWarehouseAction.DepositItem)
        {
            Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == uid);
            Assert.Contains(f.Current.PlayerItems.WarehouseItems, row => row.Uid == uid && row.WarehouseId == 8);
            Assert.False(f.Native.Inventory!.ClientItems.ContainsKey(uid));
        }
        else
        {
            Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == uid);
            Assert.DoesNotContain(f.Current.PlayerItems.WarehouseItems, row => row.Uid == uid);
            Assert.True(f.Native.Inventory!.ClientItems.ContainsKey(uid));
        }
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private sealed partial class SqlWorldFixture
    {
        internal WorldDataCatalog VendorAuthorityCatalog => catalog;
    }
}
