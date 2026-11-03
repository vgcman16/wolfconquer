using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private sealed record SqlVendorListingState(bool Cp, long Price, uint Booth,
        MsgItemUsagePacket Add, MsgItemUsagePacket Buy, SqlTradeBefore Before, string[] Accounts);

    private static async Task SendSqlVendorAsync(SqlWorldFixture f, bool seller, byte[] bytes)
    {
        var peer = Assert.IsType<RootLoopbackFramePeer>(seller ? f.Peer : f.OtherPeer);
        await f.Handler.HandleFrameAsync(peer.Connection, ReadFrame(bytes));
        await f.DrainTradeAsync(); // Both genuine peers plus retained pre-wire assertion failures.
    }

    private static async Task<SqlVendorListingState> StartSqlVendorListingAsync(SqlWorldFixture f, bool cp)
    {
        var initial = SqlTradeBefore.Capture(f); var accounts = await f.Database.SnapshotAsync();
        void RequireUnpaidInventory()
        {
            Assert.Equal(initial.First.Character.Money, f.Current.Character.Money);
            Assert.Equal(initial.Second.Character.Money, f.Other.Character.Money);
            Assert.Equal(initial.First.Character.ConquerPoints, f.Current.Character.ConquerPoints);
            Assert.Equal(initial.Second.Character.ConquerPoints, f.Other.Character.ConquerPoints);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(initial.First.PlayerItems), File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(initial.Second.PlayerItems), File.ReadAllBytes(f.OtherItemsPath));
            Assert.Equal(initial.Registry, File.ReadAllBytes(f.Data.State));
            AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        }
        Assert.Null(f.Current.LegacyVendorListings);
        int startAt = f.Sent.Count;
        await SendSqlVendorAsync(f, true, new MsgActionPacket
        { ObjId = SqlWorldFixture.FirstUid, Type = MsgActionType.StartVendor }.ToFrameBytes());
        var reply = Assert.Single(f.Sent.Skip(startAt), bytes =>
            MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Type == MsgActionType.StartVendor);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(reply), out var started));
        Assert.NotEqual(0U, started.DwParam);
        Assert.Equal(started.DwParam.ToString(CultureInfo.InvariantCulture), f.Current.Character.Attributes["RuntimeVendorUid"]);
        Assert.Empty(Assert.IsType<LegacyVendorListingBook>(f.Current.LegacyVendorListings).Listings);
        RequireUnpaidInventory();

        long price = cp ? 3 : 25;
        var add = new MsgItemUsagePacket(SqlWorldFixture.SoldUid, checked((ulong)price),
            cp ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold, 3210, 0, 0, 0);
        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, true, add.ToFrameBytes());
        Assert.Equal(add.ToFrameBytes(), Assert.Single(f.Sent)); Assert.Empty(f.OtherSent);
        Assert.True(f.Current.LegacyVendorListings!.TryGetListing(SqlWorldFixture.SoldUid, out var listing));
        Assert.Equal(price, listing.Amount); Assert.Equal(cp ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, listing.CostMode);
        RequireUnpaidInventory();
        var before = SqlTradeBefore.Capture(f);

        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, false, new MsgItemUsagePacket(started.DwParam, 0, MsgItemUsageAction.ShowVendingList, 3234, 0, 0, 0).ToFrameBytes());
        Assert.Empty(f.Sent);
        Assert.True(LegacyItemViewPacket.TryParse(ReadFrame(Assert.Single(f.OtherSent)), out var view));
        Assert.Equal(SqlWorldFixture.SoldUid, view.Uid); Assert.Equal(started.DwParam, view.PlayerUid);
        Assert.Equal(price, view.Cost); Assert.Equal((ushort)(cp ? LegacyItemViewActionMode.CPs : LegacyItemViewActionMode.Gold), view.Mode);
        Assert.Equal(SqlWorldFixture.ItemId, view.ItemId); Assert.Equal((byte)0, view.Bound);
        Assert.Same(before.First, f.Current); Assert.Same(before.Second, f.Other); f.Data.AssertFiles(before.Files);
        RequireUnpaidInventory(); Assert.Equal(accounts, await f.Database.SnapshotAsync());
        f.Sent.Clear(); f.OtherSent.Clear();
        return new(cp, price, started.DwParam, add,
            new MsgItemUsagePacket(SqlWorldFixture.SoldUid, started.DwParam, MsgItemUsageAction.BuyVendingItem, 3456, 2, 3, 4),
            before, accounts);
    }

    private static async Task AssertSqlVendorReplayAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        var files = f.Data.Files(); var seller = f.Current; var buyer = f.Other;
        f.Sent.Clear(); f.OtherSent.Clear();
        await SendSqlVendorAsync(f, true, listing.Add.ToFrameBytes()); // Seller no longer owns the listed physical UID.
        await SendSqlVendorAsync(f, false, listing.Buy.ToFrameBytes());
        Assert.Empty(f.Sent); Assert.Empty(f.OtherSent);
        Assert.Same(seller, f.Current); Assert.Same(buyer, f.Other); f.Data.AssertFiles(files);
        AssertSqlVendorPaidState(f, listing);
    }
}
