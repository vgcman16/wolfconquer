namespace WolfConquer.Core.World;

internal enum LegacyVendorRequestAction
{
    ShowListings,
    AddListingGold,
    RemoveListing,
    BuyListing,
    AddListingConquerPoints,
}

internal readonly record struct LegacyVendorRequest(
    LegacyVendorRequestAction Action,
    uint ItemUid,
    ulong Price,
    uint? BoothUid,
    uint TimestampToken);

internal interface ILegacyVendorResponseAdapter
{
    string TransportName { get; }

    byte[] CreateAcceptedResponse();

    byte[] CreateSellerRemovalResponse();
}

internal static class LegacyVendorTransportAdapters
{
    internal static bool TryAdaptRootD11(
        MsgItemOperationPacket packet,
        out LegacyVendorRequest request,
        out ILegacyVendorResponseAdapter responses,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(packet);

        request = default;
        responses = default!;
        reason = string.Empty;

        switch (packet.Action)
        {
            case MsgItemOperationAction.AddVendingItemGold when packet.HasPrice && !packet.HasSecondaryValue:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.AddListingGold,
                    packet.ItemUid,
                    packet.Price,
                    BoothUid: null,
                    packet.TimestampToken);
                break;
            case MsgItemOperationAction.AddVendingItemConquerPoints
                when !packet.HasPrice && packet.HasSecondaryValue:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.AddListingConquerPoints,
                    packet.ItemUid,
                    packet.SecondaryValue,
                    BoothUid: null,
                    packet.TimestampToken);
                break;
            case MsgItemOperationAction.RemoveVendingItem when !packet.HasPrice && packet.HasSecondaryValue:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.RemoveListing,
                    packet.ItemUid,
                    Price: 0,
                    packet.SecondaryValue,
                    packet.TimestampToken);
                break;
            case MsgItemOperationAction.BuyVendingItem when !packet.HasPrice && packet.HasSecondaryValue:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.BuyListing,
                    packet.ItemUid,
                    Price: 0,
                    packet.SecondaryValue,
                    packet.TimestampToken);
                break;
            default:
                reason = "unsupported-root-d11-vendor-shape";
                return false;
        }

        responses = new RootD11LegacyVendorResponseAdapter(packet);
        return true;
    }

    internal static bool TryAdaptVisibleDx8D13(
        MsgItemUsagePacket packet,
        out LegacyVendorRequest request,
        out ILegacyVendorResponseAdapter responses,
        out string reason)
    {
        request = default;
        responses = default!;
        reason = string.Empty;

        switch (packet.Action)
        {
            case MsgItemUsageAction.ShowVendingList:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.ShowListings,
                    ItemUid: 0,
                    Price: 0,
                    BoothUid: packet.Id,
                    packet.Timestamp);
                break;
            case MsgItemUsageAction.AddVendingItemGold:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.AddListingGold,
                    packet.Id,
                    packet.DwParam,
                    BoothUid: null,
                    packet.Timestamp);
                break;
            case MsgItemUsageAction.AddVendingItemConquerPoints:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.AddListingConquerPoints,
                    packet.Id,
                    packet.DwParam,
                    BoothUid: null,
                    packet.Timestamp);
                break;
            case MsgItemUsageAction.RemoveVendingItem:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.RemoveListing,
                    packet.Id,
                    Price: 0,
                    BoothUid: null,
                    packet.Timestamp);
                break;
            case MsgItemUsageAction.BuyVendingItem when packet.DwParam <= uint.MaxValue:
                request = new LegacyVendorRequest(
                    LegacyVendorRequestAction.BuyListing,
                    packet.Id,
                    Price: 0,
                    BoothUid: checked((uint)packet.DwParam),
                    packet.Timestamp);
                break;
            case MsgItemUsageAction.BuyVendingItem:
                reason = "booth-uid-outside-uint32";
                return false;
            default:
                reason = "unsupported-visible-dx8-d13-vendor-action";
                return false;
        }

        responses = new VisibleDx8D13LegacyVendorResponseAdapter(packet);
        return true;
    }

    private sealed class RootD11LegacyVendorResponseAdapter(MsgItemOperationPacket packet)
        : ILegacyVendorResponseAdapter
    {
        public string TransportName => "root-d11-msg-item-operation";

        public byte[] CreateAcceptedResponse()
        {
            return packet.ToFrameBytes();
        }

        public byte[] CreateSellerRemovalResponse()
        {
            return MsgItemOperationPacket.WithSecondaryValue(
                    packet.ItemUid,
                    packet.SecondaryValue,
                    MsgItemOperationAction.RemoveVendingItem,
                    packet.TimestampToken)
                .ToFrameBytes();
        }
    }

    private sealed class VisibleDx8D13LegacyVendorResponseAdapter(MsgItemUsagePacket packet)
        : ILegacyVendorResponseAdapter
    {
        public string TransportName => "visible-dx8-d13-msg-item-usage";

        public byte[] CreateAcceptedResponse()
        {
            return packet.ToFrameBytes();
        }

        public byte[] CreateSellerRemovalResponse()
        {
            return (packet with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes();
        }
    }
}
