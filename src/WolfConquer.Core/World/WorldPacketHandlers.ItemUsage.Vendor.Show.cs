using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleVisibleDx8D13VendorShowAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
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

        LegacyVendorShowSnapshot? snapshot = null;
        var reason = string.Empty;
        var now = (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime;
        lock (LegacyVendorMutationGate)
        {
            if (!sessions.TryGet(context.Connection, out var currentSession) || currentSession is null)
            {
                reason = "requester-session-missing";
            }
            else if (request.BoothUid is not { } boothUid || boothUid == 0)
            {
                reason = "booth-uid-missing";
            }
            else if (!LegacyVendorSessionResolver.TryResolveSeller(
                    sessions,
                    boothUid,
                    currentSession.Character.MapId,
                    out var sellerResolution))
            {
                reason = "seller-vendor-session-not-found";
            }
            else if (LegacyGetDistance(
                    currentSession.Character.X,
                    currentSession.Character.Y,
                    sellerResolution.BoothLocation.X,
                    sellerResolution.BoothLocation.Y) > LegacyViewThreshold)
            {
                reason = "seller-booth-not-visible";
            }
            else if (sellerResolution.ActiveSession.Session.LegacyVendorListings is not { } listings)
            {
                reason = "seller-listing-book-missing";
            }
            else if (!TryCreateLegacyVendorShowSnapshot(
                    boothUid,
                    sellerResolution.ActiveSession.Session,
                    listings,
                    context.WorldData?.ItemsById,
                    now,
                    out snapshot,
                    out reason))
            {
                reason = reason.Length == 0 ? "listing-snapshot-failed" : reason;
            }
        }

        if (snapshot is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason)
                .ConfigureAwait(false);
            return;
        }

        foreach (var itemViewFrame in snapshot.ItemViewFrames)
        {
            await context.Connection.SendAsync(itemViewFrame, cancellationToken).ConfigureAwait(false);
        }

        if (snapshot.ItemExtraFrame is { } itemExtraFrame)
        {
            await context.Connection.SendAsync(itemExtraFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent vendor listing snapshot " +
                $"{FormatSession(context with { Session = session })} transport={responses.TransportName} " +
                $"vendorUid={snapshot.BoothUid} sellerUid={snapshot.SellerUid} " +
                $"listingCount={snapshot.ItemViewFrames.Count} hasItemExtras={snapshot.ItemExtraFrame is not null}.")
            .ConfigureAwait(false);
    }

    private static bool TryCreateLegacyVendorShowSnapshot(
        uint boothUid,
        WorldSession seller,
        LegacyVendorListingBook listings,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        DateTime now,
        out LegacyVendorShowSnapshot? snapshot,
        out string reason)
    {
        snapshot = null;
        reason = string.Empty;

        uint sellerUid;
        try
        {
            sellerUid = checked((uint)seller.Character.Uid);
        }
        catch (OverflowException)
        {
            reason = "seller-uid-outside-uint32";
            return false;
        }

        var itemViewFrames = new List<byte[]>(listings.Count);
        var purifications = new List<LegacyItemExtraPurification>();
        var refineries = new List<LegacyItemExtraRefinery>();
        foreach (var listing in listings.Listings)
        {
            var matchingItems = seller.PlayerItems.InventoryItems
                .Where(item => item.Uid == listing.ItemUid)
                .Take(2)
                .ToArray();
            if (matchingItems.Length != 1)
            {
                reason = matchingItems.Length == 0
                    ? "seller-listing-item-missing"
                    : "seller-listing-item-uid-not-unique";
                return false;
            }

            var mode = listing.CostMode switch
            {
                LegacyVendorListingCostMode.Gold => LegacyItemViewActionMode.Gold,
                LegacyVendorListingCostMode.CPs => LegacyItemViewActionMode.CPs,
                _ => (LegacyItemViewActionMode)0,
            };
            if (mode == 0 || listing.Amount < 0)
            {
                reason = mode == 0 ? "listing-currency-mode-invalid" : "listing-price-negative";
                return false;
            }

            var item = matchingItems[0];
            try
            {
                itemViewFrames.Add(
                    LegacyItemViewPacket.FromSnapshotItem(
                            boothUid,
                            listing.Amount,
                            item,
                            mode,
                            LegacyGameItemPacket.CreateRuneSpellOverride(item, itemsById))
                        .ToFrameBytes());
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
                reason = "item-view-serialization-failed:" + ex.GetType().Name;
                return false;
            }

            if (!LegacyItemExtraPacket.TryCreate(item, now, out var itemExtra))
            {
                continue;
            }

            purifications.AddRange(itemExtra.Purifications);
            refineries.AddRange(itemExtra.Refineries);
        }

        var itemExtraFrame = purifications.Count + refineries.Count == 0
            ? null
            : new LegacyItemExtraPacket(purifications.ToArray(), refineries.ToArray()).ToFrameBytes();
        snapshot = new LegacyVendorShowSnapshot(
            boothUid,
            sellerUid,
            Array.AsReadOnly(itemViewFrames.ToArray()),
            itemExtraFrame);
        return true;
    }

    private sealed record LegacyVendorShowSnapshot(
        uint BoothUid,
        uint SellerUid,
        IReadOnlyList<byte[]> ItemViewFrames,
        byte[]? ItemExtraFrame);
}
