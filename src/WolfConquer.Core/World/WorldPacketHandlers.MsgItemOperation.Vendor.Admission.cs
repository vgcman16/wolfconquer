using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Vendor policy is intentionally separate from Trade's locked-partner exception.
    private static bool IsLegacyVendorClosureTradable(LegacyItemSaveGraph.Closure closure) =>
        closure.AllSourceRows(static row => row.Bound == 0 && row.Inscribed != 1 && row.Locked == 0 && row.Suspicious == 0 &&
            !LegacyNativeItemRestrictions.IsUnableTradeItem(row.ItemId));

    // Ordinary items never query optional PIN state. A protected descendant is not an escape hatch.
    private static bool LegacyVendorClosureNeedsPin(WorldSession seller, LegacyItemSaveGraph.Closure closure) =>
        closure.AnySourceRow(IsLegacySecurityAnimaProtectedItem) && LegacySecurityAnimaItemOperationNeedsPin(seller);

    private static bool TryCaptureLegacyVendorIdentity(WorldSessionStore sessions, ConquerTcpConnection connection,
        WorldSession expected, out object identity) =>
        LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, expected.Character.Uid, out identity) &&
        LegacyNativeClientPublication.TryCurrent(sessions, connection, expected.Character.Uid, identity, out var current) &&
        ReferenceEquals(current, expected);

    private static bool IsLegacyVendorCurrentOwner(WorldSessionStore sessions, ConquerTcpConnection connection,
        WorldSession expected, object identity) =>
        LegacyNativeClientPublication.TryCurrent(sessions, connection, expected.Character.Uid, identity, out var current) &&
        ReferenceEquals(current, expected) && ReferenceEquals(connection.NativeGameClient, expected.NativeGameClient);

    private static bool TryAddLegacyVendorListing(WorldPacketHandlerContext context, LegacyVendorRequest request,
        out WorldSession? updated, out LegacyVendorListing? added, out WorldSession? pinBlocked, out string reason)
    {
        updated = null;
        added = null;
        pinBlocked = null;
        reason = "current-session-missing";
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var current) || current is null ||
            !TryCaptureLegacyVendorIdentity(sessions, context.Connection, current, out var identity)) return false;
        reason = "vendor-not-active";
        if (!TryReadLegacyVendorState(current.Character, out _)) return false;
        reason = "vendor-listing-book-missing";
        if (current.LegacyVendorListings is null) return false;
        reason = "vendor-item-graph-invalid";
        if (!LegacyItemSaveGraph.TryCapture(current.PlayerItems, out var items)) return false;
        reason = "inventory-item-missing";
        if (!items.TryGetInventory(request.ItemUid, out var incoming)) return false;
        reason = "listing-item-not-tradable";
        if (!IsLegacyVendorClosureTradable(incoming)) return false;
        reason = "anima-pin-not-activated";
        if (LegacyVendorClosureNeedsPin(current, incoming)) { pinBlocked = current; return false; }
        reason = "listing-item-metadata-invalid";
        if (!TryGetLegacyVendorMetadata(context, context.Connection, current, incoming, out _)) return false;
        reason = "price-outside-int64";
        if (request.Price > long.MaxValue) return false;
        reason = "conquer-point-price-outside-uint32";
        if (request.Action == LegacyVendorRequestAction.AddListingConquerPoints && request.Price > uint.MaxValue) return false;
        var costMode = request.Action == LegacyVendorRequestAction.AddListingGold
            ? LegacyVendorListingCostMode.Gold : LegacyVendorListingCostMode.CPs;
        return sessions.TryPublishVendorListing(context, current, identity, items, incoming,
            costMode, checked((long)request.Price), out updated, out added, out reason);
    }

    internal static bool IsLegacyVendorListingAdmissionCurrent(WorldPacketHandlerContext context, WorldSession current,
        object identity, LegacyItemSaveGraph items, LegacyItemSaveGraph.Closure incoming) =>
        context.Sessions is { } sessions && IsLegacyVendorCurrentOwner(sessions, context.Connection, current, identity) &&
        context.Trades?.HasConnectionEnded(context.Connection) != true && IsLegacyVendorClosureTradable(incoming) &&
        !LegacyVendorClosureNeedsPin(current, incoming) && TryReadLegacyVendorState(current.Character, out _) &&
        TryGetLegacyVendorMetadata(context, context.Connection, current, incoming, out _) &&
        items.IsCurrent(current.PlayerItems);
}
