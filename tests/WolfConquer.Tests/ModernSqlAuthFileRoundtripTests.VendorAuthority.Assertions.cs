using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static void AssertSqlVendorListing(SqlWorldFixture f, SqlVendorListingState listing)
    {
        Assert.True(f.Current.LegacyVendorListings!.TryGetListing(SqlWorldFixture.SoldUid, out var actual));
        Assert.Equal(SqlWorldFixture.SoldUid, actual.ItemUid); Assert.Equal(listing.Price, actual.Amount);
        Assert.Equal(listing.Cp ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, actual.CostMode);
    }

    private static async Task AssertSqlVendorAuthorityShownAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        var before = SqlVendorAuthorityBefore.Capture(f);
        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, false, new MsgItemUsagePacket(listing.Booth, 0,
            MsgItemUsageAction.ShowVendingList, 4567, 0, 0, 0).ToFrameBytes());
        Assert.Empty(f.Sent);
        Assert.True(LegacyItemViewPacket.TryParse(ReadFrame(Assert.Single(f.OtherSent)), out var view));
        Assert.Equal(SqlWorldFixture.SoldUid, view.Uid); Assert.Equal(SqlWorldFixture.ItemId, view.ItemId);
        Assert.Equal(listing.Booth, view.PlayerUid); Assert.Equal(listing.Price, view.Cost);
        Assert.Equal((ushort)(listing.Cp ? LegacyItemViewActionMode.CPs : LegacyItemViewActionMode.Gold), view.Mode);
        before.Unchanged(f); AssertSqlVendorListing(f, listing);
        f.Sent.Clear(); f.OtherSent.Clear();
    }

    private static async Task AssertSqlVendorAuthorityStaleAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        var before = SqlVendorAuthorityBefore.Capture(f);
        int writes = 0; Assert.Null(f.BeforeTradeWire);
        f.Sent.Clear(); f.OtherSent.Clear();
        f.BeforeTradeWire = (_, _) => writes++;
        try
        {
            foreach (var packet in new[]
            {
                new MsgItemUsagePacket(listing.Booth, 0, MsgItemUsageAction.ShowVendingList, 4567, 0, 0, 0),
                listing.Buy, listing.Buy
            })
            {
                int logStart = f.Diagnostics.Length;
                await SendSqlVendorAsync(f, false, packet.ToFrameBytes());
                Assert.Contains("seller-listed-item-incarnation-changed", f.Diagnostics[logStart..], StringComparison.Ordinal);
                Assert.Equal(0, writes); Assert.Empty(f.Sent); Assert.Empty(f.OtherSent);
                before.Unchanged(f); AssertSqlVendorListing(f, listing);
                Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
            }
        }
        finally { f.BeforeTradeWire = null; }
    }

    private static async Task<SqlVendorListingState> RelistSqlVendorAuthorityAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        var before = SqlVendorAuthorityBefore.Capture(f);
        var remove = new MsgItemUsagePacket(SqlWorldFixture.SoldUid, 0,
            MsgItemUsageAction.RemoveVendingItem, 4678, 0, 0, 0);
        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, true, remove.ToFrameBytes());
        Assert.Equal(remove.ToFrameBytes(), Assert.Single(f.Sent)); Assert.Empty(f.OtherSent);
        Assert.False(f.Current.LegacyVendorListings!.Contains(SqlWorldFixture.SoldUid));
        before.Unpaid(f);

        long price = checked(listing.Price + 1);
        var add = listing.Add with { DwParam = checked((ulong)price), Timestamp = 4789 };
        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, true, add.ToFrameBytes());
        Assert.Equal(add.ToFrameBytes(), Assert.Single(f.Sent)); Assert.Empty(f.OtherSent);
        Assert.NotSame(before.Book, f.Current.LegacyVendorListings);
        before.Unpaid(f);
        var refreshed = listing with { Price = price, Add = add, Before = SqlTradeBefore.Capture(f) };
        AssertSqlVendorListing(f, refreshed);
        Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
        f.Sent.Clear(); f.OtherSent.Clear();
        return refreshed;
    }

    private static async Task AssertSqlVendorAuthorityColdReplayAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        await AssertSqlVendorReplayAsync(f, listing);
        var sellerItems = File.ReadAllBytes(f.ItemsPath); var buyerItems = File.ReadAllBytes(f.OtherItemsPath);
        int tickets = f.TicketCount;
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(tickets + 2, f.TicketCount);
        Assert.Null(f.Current.LegacyVendorListings); Assert.Null(f.Other.LegacyVendorListings);
        AssertSqlVendorPaidState(f, listing);
        Assert.Equal(sellerItems, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(buyerItems, File.ReadAllBytes(f.OtherItemsPath));
        await AssertSqlVendorReplayAsync(f, listing);
        Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
    }

    private sealed record SqlVendorAuthorityBefore(SqlTradeBefore State, string SellerState, string BuyerState,
        LegacyVendorListingBook Book, LegacyVendorListing[] Listings, LegacyOwnedNativeItemImage SellerImage,
        LegacyOwnedNativeItemImage BuyerImage, LegacyOwnedNativeItemImage.Node Node)
    {
        internal static SqlVendorAuthorityBefore Capture(SqlWorldFixture f) => new(SqlTradeBefore.Capture(f),
            JsonSerializer.Serialize(f.Current.Character), JsonSerializer.Serialize(f.Other.Character),
            Assert.IsType<LegacyVendorListingBook>(f.Current.LegacyVendorListings), f.Current.LegacyVendorListings!.Listings.ToArray(),
            f.Native.OrdinarySpellBinding!.Items.Image, f.Other.NativeGameClient!.OrdinarySpellBinding!.Items.Image, ListedSqlVendorNode(f));

        internal void Unpaid(SqlWorldFixture f)
        {
            Assert.Equal(SellerState, JsonSerializer.Serialize(f.Current.Character));
            Assert.Equal(BuyerState, JsonSerializer.Serialize(f.Other.Character));
            f.Data.AssertFiles(State.Files);
            Assert.Equal(State.Registry, File.ReadAllBytes(f.Data.State));
            Assert.Same(SellerImage, f.Native.OrdinarySpellBinding!.Items.Image);
            Assert.Same(BuyerImage, f.Other.NativeGameClient!.OrdinarySpellBinding!.Items.Image);
            Assert.Same(Node, ListedSqlVendorNode(f));
            AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
            Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }

        internal void Unchanged(SqlWorldFixture f)
        {
            Assert.Same(State.First, f.Current); Assert.Same(State.Second, f.Other);
            Assert.Same(Book, f.Current.LegacyVendorListings);
            Assert.Equal(Listings, f.Current.LegacyVendorListings!.Listings);
            Unpaid(f);
        }
    }
}
