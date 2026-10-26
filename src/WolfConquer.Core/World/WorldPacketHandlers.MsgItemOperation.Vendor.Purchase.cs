namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyVendorBuyAsync(
        WorldPacketHandlerContext context,
        LegacyVendorRequest request,
        ILegacyVendorResponseAdapter responses,
        CancellationToken cancellationToken)
    {
        if (context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, "vendor-operation-owner-missing")
                .ConfigureAwait(false);
            return;
        }

        LegacyVendorPreparedPurchase? committed = null;
        var reason = "vendor-recovery-unavailable";
        // Frame skips its shared lease ONLY for valid profile-specific buy requests.
        // This actual lease drains earlier shared writers before any source selection.
        using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!recovery.IsBlocked)
            {
                lock (LegacyVendorMutationGate)
                {
                    if (TryPrepareLegacyVendorPurchase(context, request, responses, out var prepared, out reason))
                    {
                        if (!LegacyVendorPurchaseOwnersCurrent(context, prepared, committed: false))
                            reason = "vendor-owner-changed-before-commit";
                        else if (TryCommitLegacyVendorPurchase(context, prepared.Buyer, prepared.UpdatedBuyer,
                                     prepared.Seller, prepared.UpdatedSeller, prepared.BuyerItems, prepared.SellerItems, out reason))
                            committed = prepared;
                    }
                }
            }
        }

        if (committed is null)
        {
            if (IsLegacyTradeRecoveryRequired(reason))
                await context.Log.WriteLineAsync(
                    $"WorldServer vendor purchase requires recovery transport={responses.TransportName} " +
                    $"itemUid={request.ItemUid} reason={reason}; durable outcome is unresolved; " +
                    "any retained journal must be reconciled, not discarded or reported unchanged; " +
                    "Store publication and success sends withheld, world fenced pending recovery.").ConfigureAwait(false);
            else if (recovery.IsBlocked)
                await context.Log.WriteLineAsync(
                    $"WorldServer vendor purchase withheld itemUid={request.ItemUid} reason={reason}; " +
                    "world recovery was already blocked; no new purchase publication or success sends.").ConfigureAwait(false);
            else
                await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason).ConfigureAwait(false);
            return;
        }

        // No rejection catch surrounds publication. Send/log faults cannot undo the
        // committed listing removal or payment, and replay cannot sell the item again.
        await PublishLegacyVendorPurchaseAsync(context, request, responses, committed, cancellationToken)
            .ConfigureAwait(false);
    }
}
