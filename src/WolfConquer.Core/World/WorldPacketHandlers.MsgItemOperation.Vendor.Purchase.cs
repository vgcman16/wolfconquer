using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyVendorBuyAsync(
        WorldPacketHandlerContext context,
        LegacyVendorRequest request,
        ILegacyVendorResponseAdapter responses,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (request.BoothUid is not { } boothUid)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, "booth-uid-missing")
                .ConfigureAwait(false);
            return;
        }

        byte[] buyerOperationFrame;
        byte[] sellerOperationFrame;
        try
        {
            buyerOperationFrame = responses.CreateAcceptedResponse();
            sellerOperationFrame = responses.CreateSellerRemovalResponse();
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            await LogLegacyVendorFenceAsync(
                    context,
                    request,
                    responses.TransportName,
                    "response-serialization-failed:" + ex.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        WorldSession? updatedBuyer = null;
        WorldSession? updatedSeller = null;
        WorldActiveSession? sellerActive = null;
        PlayerItemSnapshotItem? purchasedItem = null;
        byte[]? buyerItemFrame = null;
        byte[]? sellerItemRemoveFrame = null;
        byte[]? buyerWalletFrame = null;
        byte[]? sellerWalletFrame = null;
        var reason = string.Empty;

        lock (LegacyVendorMutationGate)
        {
            if (!sessions.TryGet(context.Connection, out var buyer) || buyer is null)
            {
                reason = "buyer-session-missing";
            }
            else if (buyer.LegacyTradeActive)
            {
                reason = "buyer-in-trade";
            }
            else if (!LegacyVendorSessionResolver.TryResolveSeller(
                    sessions,
                    boothUid,
                    buyer.Character.MapId,
                    out var sellerResolution))
            {
                reason = "seller-vendor-session-not-found";
            }
            else if (LegacyGetDistance(
                    buyer.Character.X,
                    buyer.Character.Y,
                    sellerResolution.BoothLocation.X,
                    sellerResolution.BoothLocation.Y) > LegacyViewThreshold)
            {
                reason = "seller-booth-not-visible";
            }
            else if (sellerResolution.ActiveSession.Session.LegacyTradeActive)
            {
                reason = "seller-in-trade";
            }
            else if (sellerResolution.ActiveSession.Session.LegacyVendorListings is not { } sellerListings ||
                !sellerListings.TryGetListing(request.ItemUid, out var listing))
            {
                reason = "seller-listing-missing";
            }
            else
            {
                var seller = sellerResolution.ActiveSession.Session;
                var matchingItems = seller.PlayerItems.InventoryItems
                    .Where(item => item.Uid == request.ItemUid)
                    .Take(2)
                    .ToArray();
                if (matchingItems.Length != 1)
                {
                    reason = matchingItems.Length == 0
                        ? "seller-inventory-item-missing"
                        : "seller-inventory-item-uid-not-unique";
                }
                else
                {
                    var currencyMode = listing.CostMode switch
                    {
                        LegacyVendorListingCostMode.Gold => LegacyVendorPurchaseCurrencyMode.Gold,
                        LegacyVendorListingCostMode.CPs => LegacyVendorPurchaseCurrencyMode.ConquerPoints,
                        _ => (LegacyVendorPurchaseCurrencyMode)(-1),
                    };
                    var plan = LegacyVendorPurchasePlanner.Plan(
                        buyer,
                        seller,
                        matchingItems[0],
                        currencyMode,
                        listing.Amount);
                    if (!plan.Succeeded)
                    {
                        reason = "purchase-plan-rejected:" + plan.FailureReason;
                    }
                    else
                    {
                        var removeResult = sellerListings.TryRemove(request.ItemUid);
                        if (!removeResult.Removed)
                        {
                            reason = "seller-listing-remove-failed";
                        }
                        else
                        {
                            var plannedSeller = plan.UpdatedSellerSession with
                            {
                                LegacyVendorListings = removeResult.Book,
                            };
                            try
                            {
                                var buyerUid = checked((uint)plan.UpdatedBuyerSession.Character.Uid);
                                var sellerUid = checked((uint)plannedSeller.Character.Uid);
                                purchasedItem = matchingItems[0];
                                buyerItemFrame = LegacyGameItemPacket
                                    .FromSnapshotItem(purchasedItem, mode: LegacyGameItemPacket.AddItemMode)
                                    .ToFrameBytes();
                                sellerItemRemoveFrame = new MsgItemUsagePacket(
                                        request.ItemUid,
                                        DwParam: 0,
                                        MsgItemUsageAction.RemoveInventory,
                                        Timestamp: 0,
                                        DwParam2: 0,
                                        DwParam3: 0,
                                        DwParam4: 0)
                                    .ToFrameBytes();
                                var updateType = listing.CostMode == LegacyVendorListingCostMode.Gold
                                    ? LegacyMsgUpdateDataType.Money
                                    : LegacyMsgUpdateDataType.ConquerPoints;
                                var buyerBalance = listing.CostMode == LegacyVendorListingCostMode.Gold
                                    ? plan.UpdatedBuyerSession.Character.Money
                                    : plan.UpdatedBuyerSession.Character.ConquerPoints;
                                var sellerBalance = listing.CostMode == LegacyVendorListingCostMode.Gold
                                    ? plannedSeller.Character.Money
                                    : plannedSeller.Character.ConquerPoints;
                                buyerWalletFrame = new LegacyMsgUpdatePacket(
                                        buyerUid,
                                        [new LegacyMsgUpdateEntry(updateType, checked((ulong)buyerBalance))])
                                    .ToFrameBytes();
                                sellerWalletFrame = new LegacyMsgUpdatePacket(
                                        sellerUid,
                                        [new LegacyMsgUpdateEntry(updateType, checked((ulong)sellerBalance))])
                                    .ToFrameBytes();
                            }
                            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
                            {
                                reason = "response-serialization-failed:" + ex.GetType().Name;
                            }

                            if (reason.Length == 0 &&
                                !LegacyVendorPurchasePersistence.TryPersist(
                                    buyer,
                                    plan.UpdatedBuyerSession,
                                    seller,
                                    plannedSeller,
                                    context.PlayerItemsRoot,
                                    out reason))
                            {
                                reason = "persistence-failed:" + reason;
                            }

                            if (reason.Length == 0)
                            {
                                updatedBuyer = plan.UpdatedBuyerSession;
                                updatedSeller = plannedSeller;
                                sellerActive = sellerResolution.ActiveSession;
                                sessions.Set(sellerResolution.ActiveSession.Connection, plannedSeller);
                                sessions.Set(context.Connection, plan.UpdatedBuyerSession);
                            }
                        }
                    }
                }
            }
        }

        if (updatedBuyer is null ||
            updatedSeller is null ||
            sellerActive is null ||
            purchasedItem is null ||
            buyerItemFrame is null ||
            sellerItemRemoveFrame is null ||
            buyerWalletFrame is null ||
            sellerWalletFrame is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(buyerItemFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(buyerOperationFrame, cancellationToken).ConfigureAwait(false);
        await sellerActive.Connection.SendAsync(sellerOperationFrame, cancellationToken).ConfigureAwait(false);
        await sellerActive.Connection.SendAsync(sellerItemRemoveFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(buyerWalletFrame, cancellationToken).ConfigureAwait(false);
        await sellerActive.Connection.SendAsync(sellerWalletFrame, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer completed vendor purchase " +
                $"buyerUid={updatedBuyer.Character.Uid} sellerUid={updatedSeller.Character.Uid} " +
                $"transport={responses.TransportName} vendorUid={boothUid} itemUid={purchasedItem.Uid} " +
                $"timestampToken={request.TimestampToken}; persistence committed before session and packet exposure.")
            .ConfigureAwait(false);
    }

}
