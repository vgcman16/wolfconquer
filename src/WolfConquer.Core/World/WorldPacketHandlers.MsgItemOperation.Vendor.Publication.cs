namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool LegacyVendorPurchaseOwnersCurrent(WorldPacketHandlerContext context,
        LegacyVendorPreparedPurchase purchase, bool committed)
    {
        if (context.Sessions is not { } sessions || context.Trades is not { } trades) return false;
        var buyer = committed ? purchase.UpdatedBuyer : purchase.Buyer;
        var seller = committed ? purchase.UpdatedSeller : purchase.Seller.Session;
        return !trades.HasConnectionEnded(context.Connection) && !trades.HasConnectionEnded(purchase.Seller.Connection) &&
            IsLegacyVendorCurrentOwner(sessions, context.Connection, buyer, purchase.BuyerIdentity) &&
            IsLegacyVendorCurrentOwner(sessions, purchase.Seller.Connection, seller, purchase.SellerIdentity);
    }

    private static async ValueTask PublishLegacyVendorPurchaseAsync(WorldPacketHandlerContext context,
        LegacyVendorRequest request, ILegacyVendorResponseAdapter responses,
        LegacyVendorPreparedPurchase purchase, CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
            $"WorldServer committed vendor purchase buyerUid={purchase.UpdatedBuyer.Character.Uid} " +
            $"sellerUid={purchase.UpdatedSeller.Character.Uid} itemUid={request.ItemUid}; " +
            "payment, item transfer and listing removal committed; notification delivery pending, no retry refund.")
            .ConfigureAwait(false);
        foreach (var send in purchase.Sends)
        {
            if (!LegacyVendorPurchaseOwnersCurrent(context, purchase, committed: true))
            {
                await context.Log.WriteLineAsync(
                    $"WorldServer stopped committed vendor purchase publication itemUid={request.ItemUid}; " +
                    "owner retired or changed, committed payment and item transfer retained.").ConfigureAwait(false);
                return;
            }
            await send.Connection.SendAsync(send.Frame, cancellationToken).ConfigureAwait(false);
        }
        await context.Log.WriteLineAsync(
                "WorldServer completed vendor purchase " +
                $"buyerUid={purchase.UpdatedBuyer.Character.Uid} sellerUid={purchase.UpdatedSeller.Character.Uid} " +
                $"transport={responses.TransportName} vendorUid={request.BoothUid} itemUid={request.ItemUid} " +
                $"timestampToken={request.TimestampToken}; persistence committed before session and packet exposure.")
            .ConfigureAwait(false);
    }
}
