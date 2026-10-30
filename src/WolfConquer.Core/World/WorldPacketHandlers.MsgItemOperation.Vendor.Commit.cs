using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // The async Buy owner holds the real exclusive WorldOperationBarrier lease.
    // Never acquire another lease here: Frame/direct-registry callers share that owner.
    private static bool TryCommitLegacyVendorPurchase(WorldPacketHandlerContext context,
        WorldSession buyer, WorldSession updatedBuyer, WorldActiveSession sellerActive,
        WorldSession updatedSeller, LegacyItemSaveGraph buyerItems,
        LegacyItemSaveGraph sellerItems, out string reason,
        out WorldSession? finalBuyer, out WorldSession? finalSeller)
    {
        finalBuyer = null;
        finalSeller = null;
        reason = "vendorTradeOwnerUnavailable";
        if (!Monitor.IsEntered(LegacyVendorMutationGate) || context.Trades is not { } trades ||
            context.Sessions is not { } sessions || context.TradeOperations is null ||
            context.TradeRecovery is not { } recovery) return false;
        lock (trades.SyncRoot)
        {
            var seller = sellerActive.Session;
            if (!TryCaptureLegacyVendorParticipant(context, context.Connection, buyer, out var buyerOwner) ||
                !TryCaptureLegacyVendorParticipant(context, sellerActive.Connection, seller, out var sellerOwner))
            { reason = "vendorCurrentOwnerInvalid"; return false; }
            reason = "vendorItemGraphInvalid";
            if (!LegacyItemSaveGraph.TryCapture(updatedBuyer.PlayerItems, out var preparedBuyerItems) ||
                !LegacyItemSaveGraph.TryCapture(updatedSeller.PlayerItems, out var preparedSellerItems)) return false;
            if (!TryValidateLegacyVendorTransfer(buyer, updatedBuyer, seller, updatedSeller, buyerItems, sellerItems,
                    preparedBuyerItems, preparedSellerItems, out var incoming, out reason)) return false;

            var failure = string.Empty;
            var committed = sessions.TryCommitPair(context.Connection, buyer, updatedBuyer,
                sellerActive.Connection, seller, updatedSeller, () =>
                {
                    if (!ValidatePreparedLegacyVendorPair(context, buyerOwner, sellerOwner,
                            updatedBuyer, updatedSeller, buyerItems, sellerItems,
                            preparedBuyerItems, preparedSellerItems, incoming, out failure)) return false;
                    var baselineReason = string.Empty;
                    var saved = LegacyVendorPurchasePersistence.TryPersistVendorWithBaselineValidation(
                        buyer, preparedBuyerItems.WithOwnedItemsForPersistence(updatedBuyer),
                        seller, preparedSellerItems.WithOwnedItemsForPersistence(updatedSeller), context.PlayerItemsRoot,
                        () => ValidateLegacyVendorDiskBaseline(buyer, buyerItems, context.PlayerItemsRoot, out baselineReason) &&
                            ValidateLegacyVendorDiskBaseline(seller, sellerItems, context.PlayerItemsRoot, out baselineReason) &&
                            ValidatePreparedLegacyVendorPair(context, buyerOwner, sellerOwner,
                                updatedBuyer, updatedSeller, buyerItems, sellerItems,
                                preparedBuyerItems, preparedSellerItems, incoming, out baselineReason), out failure,
                        context.VendorJournalInterruption);
                    if (!saved && baselineReason.Length != 0) failure = baselineReason;
                    return saved;
                }, out finalBuyer, out finalSeller);
            reason = committed ? string.Empty : failure.Length == 0 ? "vendorPairStale" : failure;
            if (!committed && IsLegacyTradeRecoveryRequired(reason)) recovery.Block(reason);
            return committed;
        }
    }

    private static bool ValidatePreparedLegacyVendorPair(WorldPacketHandlerContext context,
        LegacyVendorParticipant buyer, LegacyVendorParticipant seller,
        WorldSession updatedBuyer, WorldSession updatedSeller,
        LegacyItemSaveGraph buyerItems, LegacyItemSaveGraph sellerItems,
        LegacyItemSaveGraph preparedBuyerItems, LegacyItemSaveGraph preparedSellerItems,
        LegacyItemSaveGraph.Closure incoming, out string reason)
    {
        reason = "vendorCurrentOwnerInvalid";
        if (!LegacyVendorParticipantCurrent(context, buyer) || !LegacyVendorParticipantCurrent(context, seller) ||
            !ReferenceEquals(updatedBuyer.NativeGameClient, buyer.Client) ||
            !ReferenceEquals(updatedSeller.NativeGameClient, seller.Client)) return false;
        reason = "vendorRecoveryUnavailable";
        if (context.TradeRecovery is not { } recovery ||
            !recovery.IsReadyFor(buyer.Session.UsersRoot, context.PlayerItemsRoot) ||
            !recovery.IsReadyFor(seller.Session.UsersRoot, context.PlayerItemsRoot)) return false;
        reason = "vendorEscrowActiveOrInvalid";
        if (!LegacyVendorParticipantHasNoEscrow(buyer) || !LegacyVendorParticipantHasNoEscrow(seller)) return false;
        reason = "vendorItemPermissionChanged";
        if (!IsLegacyVendorClosureTradable(incoming) || LegacyVendorClosureNeedsPin(seller.Session, incoming)) return false;
        reason = "vendorCurrentOwnerChanged";
        if (!LegacyVendorAttributesCurrent(buyer) || !LegacyVendorAttributesCurrent(seller)) return false;
        // Permission/attribute queries finish before the full original/prepared item proof.
        reason = "vendorItemGraphChanged";
        if (!buyerItems.IsCurrent(buyer.Session.PlayerItems) || !sellerItems.IsCurrent(seller.Session.PlayerItems) ||
            !preparedBuyerItems.IsCurrent(updatedBuyer.PlayerItems) ||
            !preparedSellerItems.IsCurrent(updatedSeller.PlayerItems)) return false;
        reason = "vendorCurrentOwnerChanged";
        if (!LegacyVendorParticipantCurrent(context, buyer) || !LegacyVendorParticipantCurrent(context, seller)) return false;
        reason = string.Empty;
        return true;
    }
}
