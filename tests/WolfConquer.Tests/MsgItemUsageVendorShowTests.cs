using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemUsageVendorShowTests
{
    [Fact]
    public async Task D13ShowOfAnEmptyActiveBoothCompletesWithoutAnEchoOrMutation()
    {
        using var harness = new D13VendorTestHarness();
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items()));
        var buyer = harness.CreateBuyer(1_000, 200, D13VendorTestHarness.Items());
        var buyerConnection = harness.CreateConnection("buyer");
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(buyerConnection, buyer);
        harness.Sessions.Set(sellerConnection, seller);

        var log = await harness.DispatchAsync(
            buyerConnection,
            buyer,
            new MsgItemUsagePacket(
                D13VendorTestHarness.SellerBoothUid,
                DwParam: 0,
                MsgItemUsageAction.ShowVendingList,
                Timestamp: 4_500,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));

        Assert.Empty(harness.Sent);
        Assert.Same(buyer, harness.StoredSession(buyerConnection));
        Assert.Same(seller, harness.StoredSession(sellerConnection));
        Assert.Contains("listingCount=0 hasItemExtras=False", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D13ShowSendsImmutableItemViewsThenOneAggregateExtrasPacket()
    {
        using var harness = new D13VendorTestHarness();
        var goldItem = D13VendorTestHarness.Item(
            90_201,
            410_339,
            purificationItemId: 800_001,
            perfectionLevel: 3);
        var cpItem = D13VendorTestHarness.Item(
            90_202,
            410_340,
            refineryEffectId: 300_4140,
            perfectionLevel: 7);
        var listings = D13VendorTestHarness.ListingBook(
            (goldItem, LegacyVendorListingCostMode.Gold, 125),
            (cpItem, LegacyVendorListingCostMode.CPs, 75));
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(goldItem, cpItem)),
            listings);
        var buyer = harness.CreateBuyer(1_000, 200, D13VendorTestHarness.Items());
        var sentCount = 0;
        var buyerConnection = harness.CreateConnection("buyer", _ =>
        {
            sentCount++;
            if (sentCount == 1)
            {
                cpItem.PerfectionLevel = 99;
                cpItem.OwnerName = "MutatedAfterSnapshot";
            }
        });
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(buyerConnection, buyer);
        harness.Sessions.Set(sellerConnection, seller);
        var request = new MsgItemUsagePacket(
            Id: D13VendorTestHarness.SellerBoothUid,
            DwParam: 0,
            MsgItemUsageAction.ShowVendingList,
            Timestamp: 4_567,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);

        await harness.DispatchAsync(buyerConnection, buyer, request);

        Assert.Equal(99U, cpItem.PerfectionLevel);
        Assert.Collection(
            harness.Sent,
            frame => AssertItemView(
                frame,
                goldItem.Uid,
                cost: 125,
                LegacyItemViewActionMode.Gold,
                perfectionLevel: 3,
                ownerName: "OriginalOwner"),
            frame => AssertItemView(
                frame,
                cpItem.Uid,
                cost: 75,
                LegacyItemViewActionMode.CPs,
                perfectionLevel: 7,
                ownerName: "OriginalOwner"),
            frame =>
            {
                Assert.Equal("buyer", frame.Recipient);
                Assert.True(LegacyItemExtraPacket.TryParse(
                    D13VendorTestHarness.ReadFrame(frame.Bytes),
                    out var extras));
                Assert.Single(extras.Purifications);
                Assert.Equal(goldItem.Uid, extras.Purifications[0].ItemUid);
                Assert.Single(extras.Refineries);
                Assert.Equal(cpItem.Uid, extras.Refineries[0].ItemUid);
            });
        Assert.DoesNotContain(
            harness.Sent,
            frame => D13VendorTestHarness.ReadFrame(frame.Bytes).Header.Opcode == MsgItemUsagePacket.Opcode);
        Assert.Same(buyer, harness.StoredSession(buyerConnection));
        Assert.Same(seller, harness.StoredSession(sellerConnection));
    }

    [Fact]
    public async Task D13ShowRejectsAResolvedBoothOutsideVisibilityWithoutPartialResponse()
    {
        using var harness = new D13VendorTestHarness();
        var listedItem = D13VendorTestHarness.Item(90_203, 410_341);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(listedItem)),
            D13VendorTestHarness.ListingBook(
                (listedItem, LegacyVendorListingCostMode.Gold, 125)));
        var distantBuyer = harness.CreateBuyer(
            1_000,
            200,
            D13VendorTestHarness.Items(),
            x: 100);
        var buyerConnection = harness.CreateConnection("buyer");
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(buyerConnection, distantBuyer);
        harness.Sessions.Set(sellerConnection, seller);

        var log = await harness.DispatchAsync(
            buyerConnection,
            distantBuyer,
            new MsgItemUsagePacket(
                D13VendorTestHarness.SellerBoothUid,
                DwParam: 0,
                MsgItemUsageAction.ShowVendingList,
                Timestamp: 5_678,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));

        Assert.Empty(harness.Sent);
        Assert.Contains("reason=seller-booth-not-visible", log, StringComparison.Ordinal);
        Assert.Same(distantBuyer, harness.StoredSession(buyerConnection));
        Assert.Same(seller, harness.StoredSession(sellerConnection));
    }

    [Fact]
    public async Task D13ShowRejectsAStaleListingWithoutSendingAPartialList()
    {
        using var harness = new D13VendorTestHarness();
        var presentItem = D13VendorTestHarness.Item(90_204, 410_342);
        var missingItem = D13VendorTestHarness.Item(90_205, 410_343);
        var seller = D13VendorTestHarness.ActivateSeller(
            harness.CreateSeller(300, 50, D13VendorTestHarness.Items(presentItem)),
            D13VendorTestHarness.ListingBook(
                (presentItem, LegacyVendorListingCostMode.Gold, 125),
                (missingItem, LegacyVendorListingCostMode.CPs, 75)));
        var buyer = harness.CreateBuyer(1_000, 200, D13VendorTestHarness.Items());
        var buyerConnection = harness.CreateConnection("buyer");
        var sellerConnection = harness.CreateConnection("seller");
        harness.Sessions.Set(buyerConnection, buyer);
        harness.Sessions.Set(sellerConnection, seller);

        var log = await harness.DispatchAsync(
            buyerConnection,
            buyer,
            new MsgItemUsagePacket(
                D13VendorTestHarness.SellerBoothUid,
                DwParam: 0,
                MsgItemUsageAction.ShowVendingList,
                Timestamp: 6_789,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0));

        Assert.Empty(harness.Sent);
        Assert.Contains("reason=seller-listing-item-missing", log, StringComparison.Ordinal);
    }

    private static void AssertItemView(
        D13VendorSentFrame frame,
        uint itemUid,
        long cost,
        LegacyItemViewActionMode mode,
        uint perfectionLevel,
        string ownerName)
    {
        Assert.Equal("buyer", frame.Recipient);
        Assert.True(LegacyItemViewPacket.TryParse(
            D13VendorTestHarness.ReadFrame(frame.Bytes),
            out var packet));
        Assert.Equal(itemUid, packet.Uid);
        Assert.Equal(D13VendorTestHarness.SellerBoothUid, packet.PlayerUid);
        Assert.Equal(cost, packet.Cost);
        Assert.Equal((ushort)mode, packet.Mode);
        Assert.Equal(perfectionLevel, packet.PerfectionLevel);
        Assert.Equal(ownerName, packet.OwnerName);
    }
}
