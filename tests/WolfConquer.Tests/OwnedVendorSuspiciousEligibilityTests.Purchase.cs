using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedVendorSuspiciousEligibilityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ValidOwnedPurchaseKeepsUnselectedSuspiciousInventoryAndPublishesOnlyTheCommittedSixFrames(bool cp) =>
        Run([Item(ListedUid, SashType, children: [Item(ChildUid)]), Item(SellerKeptUid, suspicious: 1)],
            [Item(BuyerKeptUid, suspicious: 1)], async f =>
        {
            var booth = await Start(f); await List(f, cp);
            var before = Before.Capture(f); var selected = Assert.Single(before.Seller.PlayerItems.InventoryItems, row => row.Uid == ListedUid);
            var expectedSeller = before.Seller.PlayerItems with { InventoryItems = before.Seller.PlayerItems.InventoryItems
                .Where(row => row.Uid != ListedUid).ToArray() };
            var expectedBuyer = before.Buyer.PlayerItems with { InventoryItems = before.Buyer.PlayerItems.InventoryItems.Append(selected).ToArray() };
            var sellerBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedSeller);
            var buyerBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyer);
            var request = Purchase(booth); var delivered = new List<uint>();
            f.BeforeSend = (uid, _) =>
            {
                AssertSettled(f, before, sellerBytes, buyerBytes, cp);
                delivered.Add(uid);
            };
            try { await f.SendAsync(Fixture.Peer, request.ToFrameBytes()); }
            finally { f.BeforeSend = null; }

            Assert.Equal(new[] { Fixture.Peer, Fixture.Peer, Fixture.Actor, Fixture.Actor, Fixture.Peer, Fixture.Actor }, delivered);
            AssertSettled(f, before, sellerBytes, buyerBytes, cp);
            var buyerFrames = f.Frames(Fixture.Peer).Skip(before.BuyerFrames).ToArray();
            var sellerFrames = f.ActorFrames.Skip(before.SellerFrames).ToArray();
            Assert.Equal(3, buyerFrames.Length); Assert.Equal(3, sellerFrames.Length);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(selected, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes(), buyerFrames[0]);
            Assert.Equal(request.ToFrameBytes(), buyerFrames[1]);
            Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), sellerFrames[0]);
            Assert.Equal(new MsgItemUsagePacket(ListedUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), sellerFrames[1]);
            var currency = cp ? LegacyMsgUpdateDataType.ConquerPoints : LegacyMsgUpdateDataType.Money;
            Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Peer, [new(currency, checked((ulong)(cp ? 175L : 1875L)))]).ToFrameBytes(), buyerFrames[2]);
            Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Actor, [new(currency, checked((ulong)(cp ? 125L : 1125L)))]).ToFrameBytes(), sellerFrames[2]);
            var committed = Before.Capture(f);
            await f.SendAsync(Fixture.Peer, request.ToFrameBytes());
            committed.AssertUnchanged(f);
        });

    private static void AssertSettled(Fixture f, Before before, byte[] sellerBytes, byte[] buyerBytes, bool cp)
    {
        AssertActors(f); var seller = f.Current(); var buyer = f.Current(Fixture.Peer);
        Assert.Equal(sellerBytes, PlayerItemSnapshotLoader.ToLegacyBytes(seller.PlayerItems));
        Assert.Equal(buyerBytes, PlayerItemSnapshotLoader.ToLegacyBytes(buyer.PlayerItems));
        Assert.Equal(before.Seller.Character.Money + (cp ? 0 : 125), seller.Character.Money);
        Assert.Equal(before.Buyer.Character.Money - (cp ? 0 : 125), buyer.Character.Money);
        Assert.Equal(before.Seller.Character.ConquerPoints + (cp ? 25 : 0), seller.Character.ConquerPoints);
        Assert.Equal(before.Buyer.Character.ConquerPoints - (cp ? 25 : 0), buyer.Character.ConquerPoints);
        Assert.Equal(before.Seller.Character.BoundConquerPoints, seller.Character.BoundConquerPoints);
        Assert.Equal(before.Buyer.Character.BoundConquerPoints, buyer.Character.BoundConquerPoints);
        Assert.Equal(before.Seller.Character.BankConquerPoints, seller.Character.BankConquerPoints);
        Assert.Equal(before.Buyer.Character.BankConquerPoints, buyer.Character.BankConquerPoints);
        Assert.Equal((byte)1, Assert.Single(seller.PlayerItems.InventoryItems, row => row.Uid == SellerKeptUid).Suspicious);
        Assert.Equal((byte)1, Assert.Single(buyer.PlayerItems.InventoryItems, row => row.Uid == BuyerKeptUid).Suspicious);
        Assert.False(seller.LegacyVendorListings!.Contains(ListedUid));
        var all = seller.PlayerItems.InventoryItems.Concat(buyer.PlayerItems.InventoryItems)
            .SelectMany(row => row.DepositedItems.Select(child => child.Uid).Prepend(row.Uid)).ToArray();
        Assert.Equal(new[] { ListedUid, ChildUid, SellerKeptUid, BuyerKeptUid }.Order(), all.Order());
        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
    }
}
