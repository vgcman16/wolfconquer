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
        if (context.Sessions is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }
        byte[] acceptedResponse;
        try { acceptedResponse = responses.CreateAcceptedResponse(); }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName,
                    "response-serialization-failed:" + ex.GetType().Name).ConfigureAwait(false);
            return;
        }
        WorldSession? updatedSession;
        LegacyVendorListing? addedListing;
        WorldSession? pinBlocked;
        string reason;
        lock (LegacyVendorMutationGate)
            TryAddLegacyVendorListing(context, request, out updatedSession, out addedListing, out pinBlocked, out reason);

        if (pinBlocked is not null)
        {
            // This is rejection-only publication. Never resume admission after this await,
            // even if the current session or its PIN authorization changes meanwhile.
            await TryBlockLegacySecurityAnimaItemOperationAsync(context with { Session = pinBlocked }, pinBlocked,
                    request.Action == LegacyVendorRequestAction.AddListingGold
                        ? LegacyVendorAddGoldSource : LegacyVendorAddConquerPointsSource, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        if (updatedSession is null || addedListing is null)
        {
            await LogLegacyVendorFenceAsync(context, request, responses.TransportName, reason).ConfigureAwait(false);
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
