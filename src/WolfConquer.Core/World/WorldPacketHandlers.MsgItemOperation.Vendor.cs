namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyVendorAddGoldSource = "MsgItemUsuagePacket.AddVendingItemGold";
    private const string LegacyVendorAddConquerPointsSource = "MsgItemUsuagePacket.AddVendingItemConquerPts";

    private static readonly object LegacyVendorMutationGate = new();

    private static ValueTask HandleMsgItemOperationVendorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemOperationPacket packet,
        CancellationToken cancellationToken)
    {
        if (!LegacyVendorTransportAdapters.TryAdaptRootD11(
                packet,
                out var request,
                out var responses,
                out var reason))
        {
            return new ValueTask(LogMsgItemOperationVendorFenceAsync(context, packet, reason));
        }

        return HandleLegacyVendorRequestAsync(context, session, request, responses, cancellationToken);
    }

    private static ValueTask HandleLegacyVendorRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyVendorRequest request,
        ILegacyVendorResponseAdapter responses,
        CancellationToken cancellationToken)
    {
        return request.Action switch
        {
            LegacyVendorRequestAction.AddListingGold or
            LegacyVendorRequestAction.AddListingConquerPoints =>
                HandleLegacyVendorAddAsync(context, session, request, responses, cancellationToken),
            LegacyVendorRequestAction.RemoveListing =>
                HandleLegacyVendorRemoveAsync(context, request, responses, cancellationToken),
            LegacyVendorRequestAction.BuyListing =>
                HandleLegacyVendorBuyAsync(context, request, responses, cancellationToken),
            _ => ValueTask.CompletedTask,
        };
    }

    private static Task LogMsgItemOperationVendorFenceAsync(
        WorldPacketHandlerContext context,
        MsgItemOperationPacket packet,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP MsgItemOperation vendor mutation fenced " +
            $"{FormatSession(context)} action={(uint)packet.Action} itemUid={packet.ItemUid} " +
            $"price={packet.Price} secondaryValue={packet.SecondaryValue} reason={reason}; " +
            "no listing, wallet, item, persistence, session, or packet mutation was committed.");
    }

    private static Task LogLegacyVendorFenceAsync(
        WorldPacketHandlerContext context,
        LegacyVendorRequest request,
        string transportName,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP vendor operation fenced " +
            $"{FormatSession(context)} transport={transportName} action={request.Action} " +
            $"itemUid={request.ItemUid} price={request.Price} boothUid={request.BoothUid?.ToString() ?? "none"} " +
            $"reason={reason}; no listing, wallet, item, persistence, session, or packet mutation was committed.");
    }
}
