using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyVendorPurchaseSend(ConquerTcpConnection Connection, byte[] Frame);
    private sealed record LegacyVendorPreparedPurchase(WorldSession Buyer, WorldActiveSession Seller,
        WorldSession UpdatedBuyer, WorldSession UpdatedSeller, LegacyItemSaveGraph BuyerItems,
        LegacyItemSaveGraph SellerItems, object BuyerIdentity, object SellerIdentity,
        LegacyVendorPurchaseSend[] Sends);

    // Pure preparation under the vendor gate and the caller's exclusive operation lease.
    private static bool TryPrepareLegacyVendorPurchase(WorldPacketHandlerContext context,
        LegacyVendorRequest request, ILegacyVendorResponseAdapter responses,
        out LegacyVendorPreparedPurchase prepared, out string reason)
    {
        prepared = null!;
        reason = "session-store-missing";
        if (context.Sessions is not { } sessions) return false;
        reason = "booth-uid-missing";
        if (request.BoothUid is not { } boothUid) return false;
        reason = "buyer-session-missing";
        if (!sessions.TryGet(context.Connection, out var buyer) || buyer is null) return false;
        reason = "buyer-in-trade";
        if (buyer.LegacyTradeActive) return false;
        reason = "seller-vendor-session-not-found";
        if (!LegacyVendorSessionResolver.TryResolveSeller(sessions, boothUid, buyer.Character.MapId, out var resolution)) return false;
        reason = "seller-booth-not-visible";
        if (LegacyGetDistance(buyer.Character.X, buyer.Character.Y, resolution.BoothLocation.X,
                resolution.BoothLocation.Y) > LegacyViewThreshold) return false;
        var seller = resolution.ActiveSession.Session;
        reason = "seller-in-trade";
        if (seller.LegacyTradeActive) return false;
        reason = "seller-listing-missing";
        if (seller.LegacyVendorListings is not { } listings || !listings.TryGetListing(request.ItemUid, out var listing)) return false;
        reason = "vendor-owner-unavailable";
        if (!TryCaptureLegacyVendorIdentity(sessions, context.Connection, buyer, out var buyerIdentity) ||
            !TryCaptureLegacyVendorIdentity(sessions, resolution.ActiveSession.Connection, seller, out var sellerIdentity)) return false;
        reason = "vendor-item-graph-invalid";
        if (!LegacyItemSaveGraph.TryCapture(buyer.PlayerItems, out var buyerItems) ||
            !LegacyItemSaveGraph.TryCapture(seller.PlayerItems, out var sellerItems)) return false;
        reason = "seller-inventory-item-missing";
        if (!sellerItems.TryGetInventory(request.ItemUid, out var incoming)) return false;
        reason = "seller-item-not-tradable";
        if (!IsLegacyVendorClosureTradable(incoming)) return false;
        reason = "seller-anima-pin-not-activated";
        if (LegacyVendorClosureNeedsPin(seller, incoming)) return false;
        reason = "buyer-item-uid-collision";
        if (buyerItems.ContainsAny(incoming)) return false;
        var currency = listing.CostMode switch
        {
            LegacyVendorListingCostMode.Gold => LegacyVendorPurchaseCurrencyMode.Gold,
            LegacyVendorListingCostMode.CPs => LegacyVendorPurchaseCurrencyMode.ConquerPoints,
            _ => (LegacyVendorPurchaseCurrencyMode)(-1),
        };
        var plan = LegacyVendorPurchasePlanner.Plan(buyer, seller, incoming.Source, currency, listing.Amount);
        reason = "purchase-plan-rejected:" + plan.FailureReason;
        if (!plan.Succeeded) return false;
        var removal = listings.TryRemove(request.ItemUid);
        reason = "seller-listing-remove-failed";
        if (!removal.Removed) return false;
        // The public planner remains a pure economic projection. Only this mutating
        // consumer substitutes the captured detached closure for its incoming alias.
        var received = incoming.CreateInventoryItem();
        var updatedBuyer = plan.UpdatedBuyerSession with
        {
            PlayerItems = plan.UpdatedBuyerSession.PlayerItems with
            { InventoryItems = buyer.PlayerItems.InventoryItems.Append(received).ToArray() },
        };
        var updatedSeller = plan.UpdatedSellerSession with { LegacyVendorListings = removal.Book };
        try
        {
            var sends = PrepareLegacyVendorPurchaseFrames(context.Connection, resolution.ActiveSession.Connection,
                request, responses, listing, updatedBuyer, updatedSeller, received);
            prepared = new(buyer, resolution.ActiveSession, updatedBuyer, updatedSeller,
                buyerItems, sellerItems, buyerIdentity, sellerIdentity, sends);
        }
        catch (Exception error) when (error is OverflowException or ArgumentOutOfRangeException)
        {
            reason = "response-serialization-failed:" + error.GetType().Name;
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static LegacyVendorPurchaseSend[] PrepareLegacyVendorPurchaseFrames(
        ConquerTcpConnection buyerConnection, ConquerTcpConnection sellerConnection,
        LegacyVendorRequest request, ILegacyVendorResponseAdapter responses, LegacyVendorListing listing,
        WorldSession buyer, WorldSession seller, PlayerItemSnapshotItem incoming)
    {
        var gold = listing.CostMode == LegacyVendorListingCostMode.Gold;
        var type = gold ? LegacyMsgUpdateDataType.Money : LegacyMsgUpdateDataType.ConquerPoints;
        return
        [
            new(buyerConnection, LegacyGameItemPacket.FromSnapshotItem(incoming, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes()),
            new(buyerConnection, responses.CreateAcceptedResponse()),
            new(sellerConnection, responses.CreateSellerRemovalResponse()),
            new(sellerConnection, new MsgItemUsagePacket(request.ItemUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()),
            new(buyerConnection, new LegacyMsgUpdatePacket(checked((uint)buyer.Character.Uid),
                [new(type, checked((ulong)(gold ? buyer.Character.Money : buyer.Character.ConquerPoints)))]).ToFrameBytes()),
            new(sellerConnection, new LegacyMsgUpdatePacket(checked((uint)seller.Character.Uid),
                [new(type, checked((ulong)(gold ? seller.Character.Money : seller.Character.ConquerPoints)))]).ToFrameBytes()),
        ];
    }
}
