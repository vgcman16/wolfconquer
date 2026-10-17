namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyVendorAddAsync(
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

        byte[] acceptedResponse;
        try
        {
            acceptedResponse = responses.CreateAcceptedResponse();
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

        if (sessions.TryGet(context.Connection, out var storedSession) && storedSession is not null)
        {
            session = storedSession;
        }

        var protectedItem = session.PlayerItems.InventoryItems
            .FirstOrDefault(item => item.Uid == request.ItemUid);
        if (protectedItem is not null &&
            IsLegacySecurityAnimaProtectedItem(protectedItem) &&
            await TryBlockLegacySecurityAnimaItemOperationAsync(
                    context with { Session = session },
                    session,
                    request.Action == LegacyVendorRequestAction.AddListingGold
                        ? LegacyVendorAddGoldSource
                        : LegacyVendorAddConquerPointsSource,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        WorldSession? updatedSession = null;
        LegacyVendorListing? addedListing = null;
        var reason = string.Empty;
        lock (LegacyVendorMutationGate)
        {
            if (!sessions.TryGet(context.Connection, out var currentSession) || currentSession is null)
            {
                reason = "current-session-missing";
            }
            else if (!TryReadLegacyVendorState(currentSession.Character, out _))
            {
                reason = "vendor-not-active";
            }
            else if (currentSession.LegacyVendorListings is not { } listings)
            {
                reason = "vendor-listing-book-missing";
            }
            else
            {
                var matches = currentSession.PlayerItems.InventoryItems
                    .Where(item => item.Uid == request.ItemUid)
                    .Take(2)
                    .ToArray();
                if (matches.Length != 1)
                {
                    reason = matches.Length == 0 ? "inventory-item-missing" : "inventory-item-uid-not-unique";
                }
                else if (request.Price > long.MaxValue)
                {
                    reason = "price-outside-int64";
                }
                else if (request.Action == LegacyVendorRequestAction.AddListingConquerPoints &&
                    request.Price > uint.MaxValue)
                {
                    reason = "conquer-point-price-outside-uint32";
                }
                else
                {
                    var costMode = request.Action == LegacyVendorRequestAction.AddListingGold
                        ? LegacyVendorListingCostMode.Gold
                        : LegacyVendorListingCostMode.CPs;
                    var amount = checked((long)request.Price);
                    var result = listings.TryAdd(matches[0], costMode, amount);
                    if (!result.Added)
                    {
                        reason = "listing-rejected:" + result.Failure;
                    }
                    else
                    {
                        updatedSession = currentSession with { LegacyVendorListings = result.Book };
                        addedListing = result.Listing;
                        sessions.Set(context.Connection, updatedSession);
                    }
                }
            }
        }

        if (updatedSession is null || addedListing is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(acceptedResponse, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer added vendor listing " +
                $"{FormatSession(context with { Session = updatedSession })} transport={responses.TransportName} " +
                $"action={request.Action} itemUid={request.ItemUid} currency={addedListing.Value.CostMode} " +
                $"amount={addedListing.Value.Amount} timestampToken={request.TimestampToken}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyVendorRemoveAsync(
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

        byte[] acceptedResponse;
        try
        {
            acceptedResponse = responses.CreateAcceptedResponse();
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

        WorldSession? updatedSession = null;
        uint activeBoothUid = 0;
        var reason = string.Empty;
        lock (LegacyVendorMutationGate)
        {
            if (!sessions.TryGet(context.Connection, out var currentSession) || currentSession is null)
            {
                reason = "current-session-missing";
            }
            else if (!TryReadLegacyVendorState(currentSession.Character, out var vendorState))
            {
                reason = "vendor-not-active";
            }
            else if (request.BoothUid is { } requestedBoothUid && vendorState.Uid != requestedBoothUid)
            {
                reason = "vendor-uid-mismatch";
            }
            else if (currentSession.LegacyVendorListings is not { } listings)
            {
                reason = "vendor-listing-book-missing";
            }
            else
            {
                var result = listings.TryRemove(request.ItemUid);
                if (!result.Removed)
                {
                    reason = "listing-missing";
                }
                else
                {
                    activeBoothUid = vendorState.Uid;
                    updatedSession = currentSession with { LegacyVendorListings = result.Book };
                    sessions.Set(context.Connection, updatedSession);
                }
            }
        }

        if (updatedSession is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(acceptedResponse, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer removed vendor listing " +
                $"{FormatSession(context with { Session = updatedSession })} transport={responses.TransportName} " +
                $"itemUid={request.ItemUid} vendorUid={activeBoothUid} timestampToken={request.TimestampToken}.")
            .ConfigureAwait(false);
    }

}
