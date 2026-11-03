using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

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
            else if (!IsLegacyVendorMetadataPairCurrent(context, currentSession,
                sellerResolution.ActiveSession.Connection, sellerResolution.ActiveSession.Session))
            {
                reason = "seller-item-metadata-invalid";
            }
            else if (!TryCreateLegacyVendorShowSnapshot(
                    sessions,
                    sellerResolution.ActiveSession.Connection,
                    context.WorldData,
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
        WorldSessionStore sessions,
        ConquerTcpConnection sellerConnection,
        WorldDataCatalog? catalog,
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

        if (!TryCaptureLegacyVendorIdentity(sessions, sellerConnection, seller, out var identity))
        { reason = "seller-owner-changed"; return false; }
        if (!LegacyItemSaveGraph.TryCapture(seller.PlayerItems, out var graph))
        { reason = "seller-item-graph-invalid"; return false; }

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
            if (!graph.TryGetInventory(listing.ItemUid, out var captured))
            {
                reason = "seller-listing-item-missing";
                return false;
            }
            if (!listings.TryGetCurrentListing(listing.ItemUid, captured.Source, sessions, sellerConnection, seller, catalog, out _))
            { reason = "seller-listed-item-incarnation-changed"; return false; }

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

            var item = captured.Source;
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
        if (!graph.IsCurrent(seller.PlayerItems) || !IsLegacyVendorCurrentOwner(sessions, sellerConnection, seller, identity) ||
            listings.Listings.Any(listing => !graph.TryGetInventory(listing.ItemUid, out var current) ||
                !listings.TryGetCurrentListing(listing.ItemUid, current.Source, sessions, sellerConnection, seller, catalog, out _)))
        { reason = "seller-listed-item-incarnation-changed"; return false; }
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
