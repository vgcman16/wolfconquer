using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static void AssertSqlVendorPaidState(SqlWorldFixture f, SqlVendorListingState listing)
    {
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        var before = listing.Before; var seller = f.Current; var buyer = f.Other;
        Assert.False(seller.LegacyTradeActive); Assert.False(buyer.LegacyTradeActive);
        long gold = listing.Cp ? 0 : listing.Price, cp = listing.Cp ? listing.Price : 0;
        Assert.Equal(before.First.Character.Money + gold, seller.Character.Money);
        Assert.Equal(before.Second.Character.Money - gold, buyer.Character.Money);
        Assert.Equal(before.First.Character.ConquerPoints + cp, seller.Character.ConquerPoints);
        Assert.Equal(before.Second.Character.ConquerPoints - cp, buyer.Character.ConquerPoints);
        Assert.Equal(before.First.Character.Money + before.Second.Character.Money, seller.Character.Money + buyer.Character.Money);
        Assert.Equal(before.First.Character.ConquerPoints + before.Second.Character.ConquerPoints,
            seller.Character.ConquerPoints + buyer.Character.ConquerPoints);
        var incoming = Assert.Single(before.First.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
        var expectedSeller = before.First.PlayerItems with { InventoryItems = before.First.PlayerItems.InventoryItems
            .Where(row => row.Uid != SqlWorldFixture.SoldUid).ToArray() };
        var expectedBuyer = before.Second.PlayerItems with { InventoryItems = before.Second.PlayerItems.InventoryItems.Append(incoming).ToArray() };
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedSeller), PlayerItemSnapshotLoader.ToLegacyBytes(seller.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyer), PlayerItemSnapshotLoader.ToLegacyBytes(buyer.PlayerItems));
        Assert.DoesNotContain(seller.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
        var delivered = Assert.Single(buyer.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
        Assert.NotSame(incoming, delivered);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new([incoming], [], [], [], [])),
            PlayerItemSnapshotLoader.ToLegacyBytes(new([delivered], [], [], [], [])));
        var all = seller.PlayerItems.InventoryItems.Concat(buyer.PlayerItems.InventoryItems).ToArray();
        Assert.Equal(5, all.Length); Assert.Equal(all.Length, all.Select(row => row.Uid).Distinct().Count());
        Assert.Equal(before.First.PlayerItems.InventoryItems.Concat(before.Second.PlayerItems.InventoryItems).Select(row => row.Uid).Order(),
            all.Select(row => row.Uid).Order());
        Assert.Equal((byte)1, Assert.Single(seller.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.FirstBoundKept).Bound);
        Assert.Equal((byte)1, Assert.Single(buyer.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SecondBoundKept).Locked);
        foreach (var pair in new[] { (before.First, seller), (before.Second, buyer) })
        {
            Assert.Equal(pair.Item1.Character.BoundConquerPoints, pair.Item2.Character.BoundConquerPoints);
            Assert.Equal(pair.Item1.Character.BankConquerPoints, pair.Item2.Character.BankConquerPoints);
            Assert.True(LegacyTradeEscrowClaims.TryRead(pair.Item2.Character.Attributes, out var claim, out var reason), reason); Assert.Null(claim);
        }
        Assert.False(seller.LegacyVendorListings?.Contains(SqlWorldFixture.SoldUid) == true);
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
        Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { f.UserPath, f.OtherUserPath, f.ItemsPath, f.OtherItemsPath };
        foreach (var file in before.Files)
        {
            var full = Path.Combine(f.Data.Root, file.Key);
            if (!targets.Contains(full)) Assert.Equal(file.Value, File.ReadAllBytes(full));
        }
    }

    private static async Task AssertSqlVendorPurchaseAsync(SqlWorldFixture f, SqlVendorListingState listing)
    {
        Assert.Null(f.BeforeTradeWire); f.Sent.Clear(); f.OtherSent.Clear();
        var before = listing.Before;
        var sellerBinding = f.Native.OrdinarySpellBinding!;
        var buyerBinding = f.Other.NativeGameClient!.OrdinarySpellBinding!;
        var writes = new List<uint>();
        f.BeforeTradeWire = (uid, _) =>
        {
            AssertSqlVendorPaidState(f, listing);
            Assert.Empty(Assert.IsType<LegacyVendorListingBook>(f.Current.LegacyVendorListings).Listings);
            Assert.Same(sellerBinding, f.Native.OrdinarySpellBinding);
            Assert.Same(buyerBinding, f.Other.NativeGameClient!.OrdinarySpellBinding);
            Assert.NotSame(before.First, f.Current); Assert.NotSame(before.Second, f.Other);
            writes.Add(uid);
        };
        try { await SendSqlVendorAsync(f, false, listing.Buy.ToFrameBytes()); } finally { f.BeforeTradeWire = null; }
        Assert.Equal(new[] { SqlWorldFixture.SecondUid, SqlWorldFixture.SecondUid, SqlWorldFixture.FirstUid,
            SqlWorldFixture.FirstUid, SqlWorldFixture.SecondUid, SqlWorldFixture.FirstUid }, writes);
        Assert.Equal(3, f.Sent.Count); Assert.Equal(3, f.OtherSent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(f.OtherSent[0]), out var incoming));
        Assert.Equal(SqlWorldFixture.SoldUid, incoming.Uid); Assert.Equal(SqlWorldFixture.ItemId, incoming.ItemId);
        Assert.Equal((byte)0, incoming.Bound); Assert.Equal((ushort)1, incoming.StackSize);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, incoming.Mode);
        Assert.Equal(listing.Buy.ToFrameBytes(), f.OtherSent[1]);
        Assert.Equal((listing.Buy with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(RemoveGolden(SqlWorldFixture.SoldUid), f.Sent[1]);
        var type = listing.Cp ? LegacyMsgUpdateDataType.ConquerPoints : LegacyMsgUpdateDataType.Money;
        foreach (var row in new[] { (f.Sent[2], SqlWorldFixture.FirstUid, f.Current), (f.OtherSent[2], SqlWorldFixture.SecondUid, f.Other) })
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(row.Item1), out var wallet)); Assert.Equal(row.Item2, wallet.Uid);
            Assert.Equal(new[] { new LegacyMsgUpdateEntry(type, checked((ulong)(listing.Cp
                ? row.Item3.Character.ConquerPoints : row.Item3.Character.Money))) }, wallet.Updates);
        }
        AssertSqlVendorPaidState(f, listing);
        Assert.Empty(Assert.IsType<LegacyVendorListingBook>(f.Current.LegacyVendorListings).Listings);
        Assert.Equal(listing.Accounts, await f.Database.SnapshotAsync());
    }
}
