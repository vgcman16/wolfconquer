using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemUsageVendorMutationTests
{
    [Fact]
    public async Task D13VendorMutationIsFencedOutsideTheD13Profile()
    {
        using var harness = new D13VendorTestHarness();
        var item = D13VendorTestHarness.Item(90_000, 410_338);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(item)));
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(sellerConnection, seller);
        var request = new MsgItemUsagePacket(
            item.Uid,
            DwParam: 125,
            MsgItemUsageAction.AddVendingItemGold,
            Timestamp: 1_200,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);

        var log = await harness.DispatchAsync(
            sellerConnection,
            seller,
            request,
            ConquerProtocolProfile.RootD11_7899);

        Assert.Empty(harness.Sent);
        Assert.Empty(harness.StoredSession(sellerConnection).LegacyVendorListings!.Listings);
        Assert.Contains("PROTOCOL-COLLISION fenced D13 vendor packet", log, StringComparison.Ordinal);
        Assert.Contains("profile=RootD11_7899 opcode=2137 action=22", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D13GoldAddAndRemoveReuseListingStateAndEchoExactFixedPackets()
    {
        using var harness = new D13VendorTestHarness();
        var item = D13VendorTestHarness.Item(90_001, 410_339);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(item)));
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(sellerConnection, seller);
        var add = new MsgItemUsagePacket(
            item.Uid,
            DwParam: 4_000_000_000,
            MsgItemUsageAction.AddVendingItemGold,
            Timestamp: 1_234,
            DwParam2: 2,
            DwParam3: 3,
            DwParam4: 4);

        await harness.DispatchAsync(sellerConnection, seller, add);

        var listedSeller = harness.StoredSession(sellerConnection);
        Assert.True(listedSeller.LegacyVendorListings!.TryGetListing(item.Uid, out var listing));
        Assert.Equal(LegacyVendorListingCostMode.Gold, listing.CostMode);
        Assert.Equal(4_000_000_000, listing.Amount);
        Assert.Equal(add, AssertUsage(Assert.Single(harness.Sent).Bytes));

        harness.Sent.Clear();
        var remove = new MsgItemUsagePacket(
            item.Uid,
            DwParam: ulong.MaxValue,
            MsgItemUsageAction.RemoveVendingItem,
            Timestamp: 1_235,
            DwParam2: 5,
            DwParam3: 6,
            DwParam4: 7);
        await harness.DispatchAsync(sellerConnection, listedSeller, remove);

        Assert.Empty(harness.StoredSession(sellerConnection).LegacyVendorListings!.Listings);
        Assert.Equal(remove, AssertUsage(Assert.Single(harness.Sent).Bytes));
    }

    [Fact]
    public async Task D13ConquerPointAddUsesDwParamAndPreservesTheInventoryItem()
    {
        using var harness = new D13VendorTestHarness();
        var item = D13VendorTestHarness.Item(90_002, 410_340);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(item)));
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(sellerConnection, seller);
        var request = new MsgItemUsagePacket(
            item.Uid,
            DwParam: 2_000_000_000,
            MsgItemUsageAction.AddVendingItemConquerPoints,
            Timestamp: 2_345,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);

        await harness.DispatchAsync(sellerConnection, seller, request);

        var updated = harness.StoredSession(sellerConnection);
        Assert.Same(item, Assert.Single(updated.PlayerItems.InventoryItems));
        Assert.True(updated.LegacyVendorListings!.TryGetListing(item.Uid, out var listing));
        Assert.Equal(LegacyVendorListingCostMode.CPs, listing.CostMode);
        Assert.Equal(2_000_000_000, listing.Amount);
        Assert.Equal(request, AssertUsage(Assert.Single(harness.Sent).Bytes));
    }

    [Theory]
    [InlineData(MsgItemUsageAction.AddVendingItemGold, ulong.MaxValue, "price-outside-int64")]
    [InlineData(MsgItemUsageAction.AddVendingItemConquerPoints, 4_294_967_296UL, "conquer-point-price-outside-uint32")]
    public async Task D13AddRejectsPricesOutsideTheSharedPersistentRange(
        MsgItemUsageAction action,
        ulong price,
        string expectedReason)
    {
        using var harness = new D13VendorTestHarness();
        var item = D13VendorTestHarness.Item(90_003, 410_341);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(item)));
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(sellerConnection, seller);

        var log = await harness.DispatchAsync(
            sellerConnection,
            seller,
            new MsgItemUsagePacket(
                item.Uid,
                price,
                action,
                Timestamp: 2_346,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));

        Assert.Empty(harness.Sent);
        Assert.Empty(harness.StoredSession(sellerConnection).LegacyVendorListings!.Listings);
        Assert.Contains("reason=" + expectedReason, log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D13BuyRejectsABoothUidOutsideTheClientUint32Domain()
    {
        using var harness = new D13VendorTestHarness();
        var buyer = harness.CreateBuyer(1_000, 200, D13VendorTestHarness.Items());
        var buyerConnection = harness.CreateConnection("buyer");
        harness.Sessions.Set(buyerConnection, buyer);

        var log = await harness.DispatchAsync(
            buyerConnection,
            buyer,
            new MsgItemUsagePacket(
                Id: 90_004,
                DwParam: (ulong)uint.MaxValue + 1,
                MsgItemUsageAction.BuyVendingItem,
                Timestamp: 2_347,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));

        Assert.Empty(harness.Sent);
        Assert.Same(buyer, harness.StoredSession(buyerConnection));
        Assert.Contains("reason=booth-uid-outside-uint32", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D13BuyCommitsSharedTransferPersistenceAndTransportSpecificResponses()
    {
        using var harness = new D13VendorTestHarness();
        const long price = 250;
        var buyerItem = D13VendorTestHarness.Item(80_001, 410_001);
        var listedItem = D13VendorTestHarness.Item(90_101, 410_339);
        var sellerRemainder = D13VendorTestHarness.Item(90_102, 410_340);
        var buyer = harness.CreateBuyer(1_000, 200, D13VendorTestHarness.Items(buyerItem));
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(listedItem, sellerRemainder)),
            D13VendorTestHarness.ListingBook(
                (listedItem, LegacyVendorListingCostMode.Gold, price)));
        harness.SeedPersistence(buyer, seller);
        var buyerConnection = harness.CreateConnection("buyer");
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(buyerConnection, buyer);
        harness.Sessions.Set(sellerConnection, seller);
        var request = new MsgItemUsagePacket(
            listedItem.Uid,
            DwParam: D13VendorTestHarness.SellerBoothUid,
            MsgItemUsageAction.BuyVendingItem,
            Timestamp: 3_456,
            DwParam2: 2,
            DwParam3: 3,
            DwParam4: 4);

        await harness.DispatchAsync(buyerConnection, buyer, request);

        var updatedBuyer = harness.StoredSession(buyerConnection);
        var updatedSeller = harness.StoredSession(sellerConnection);
        Assert.Equal(750, updatedBuyer.Character.Money);
        Assert.Equal(550, updatedSeller.Character.Money);
        Assert.Contains(updatedBuyer.PlayerItems.InventoryItems, item => item.Uid == listedItem.Uid);
        Assert.DoesNotContain(updatedSeller.PlayerItems.InventoryItems, item => item.Uid == listedItem.Uid);
        Assert.Empty(updatedSeller.LegacyVendorListings!.Listings);

        Assert.Collection(
            harness.Sent,
            frame =>
            {
                Assert.Equal("buyer", frame.Recipient);
                Assert.True(LegacyGameItemPacket.TryParse(
                    D13VendorTestHarness.ReadFrame(frame.Bytes),
                    out var itemPacket));
                Assert.Equal(listedItem.Uid, itemPacket.Uid);
                Assert.Equal(WorldPacketOpcodes.LegacyGameItem, LegacyGameItemPacket.Opcode);
            },
            frame =>
            {
                Assert.Equal("buyer", frame.Recipient);
                Assert.Equal(request, AssertUsage(frame.Bytes));
            },
            frame =>
            {
                Assert.Equal("seller", frame.Recipient);
                Assert.Equal(
                    request with { Action = MsgItemUsageAction.RemoveVendingItem },
                    AssertUsage(frame.Bytes));
            },
            frame =>
            {
                Assert.Equal("seller", frame.Recipient);
                var removal = AssertUsage(frame.Bytes);
                Assert.Equal(listedItem.Uid, removal.Id);
                Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
            },
            frame => AssertWallet(frame, "buyer", D13VendorTestHarness.BuyerCharacterUid, 750),
            frame => AssertWallet(frame, "seller", D13VendorTestHarness.SellerCharacterUid, 550));

        Assert.Equal((750L, 200L), harness.ReadBalances(D13VendorTestHarness.BuyerAccountId));
        Assert.Equal((550L, 50L), harness.ReadBalances(D13VendorTestHarness.SellerAccountId));
        var loader = new PlayerItemSnapshotLoader(harness.PlayerItemsRoot);
        Assert.Contains(
            loader.LoadByCharacterUid(D13VendorTestHarness.BuyerCharacterUid).InventoryItems,
            item => item.Uid == listedItem.Uid);
        Assert.DoesNotContain(
            loader.LoadByCharacterUid(D13VendorTestHarness.SellerCharacterUid).InventoryItems,
            item => item.Uid == listedItem.Uid);
    }

    private static MsgItemUsagePacket AssertUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(D13VendorTestHarness.ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertWallet(
        D13VendorSentFrame frame,
        string recipient,
        uint characterUid,
        ulong balance)
    {
        Assert.Equal(recipient, frame.Recipient);
        Assert.True(LegacyMsgUpdatePacket.TryParse(
            D13VendorTestHarness.ReadFrame(frame.Bytes),
            out var packet));
        Assert.Equal(characterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Money, update.Type);
        Assert.Equal(balance, update.Value);
    }
}
