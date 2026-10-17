using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleVisibleDx8D13VendorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            await context.Log.WriteLineAsync(
                    "PROTOCOL-COLLISION fenced D13 vendor packet " +
                    $"{FormatSession(context)} profile={context.ProtocolProfile} opcode={MsgItemUsagePacket.Opcode} " +
                    $"action={(ushort)packet.Action} expectedProfile={ConquerProtocolProfile.VisibleDx8D13}; " +
                    "no listing, wallet, item, persistence, session, or packet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyVendorTransportAdapters.TryAdaptVisibleDx8D13(
                packet,
                out var request,
                out var responses,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP visible DX8 D13 vendor packet fenced " +
                    $"{FormatSession(context)} action={(ushort)packet.Action} id={packet.Id} " +
                    $"dwParam={packet.DwParam} reason={reason}; no state or packet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (request.Action == LegacyVendorRequestAction.ShowListings)
        {
            await HandleVisibleDx8D13VendorShowAsync(
                    context,
                    session,
                    request,
                    responses,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacyVendorRequestAsync(context, session, request, responses, cancellationToken)
            .ConfigureAwait(false);
    }
}
