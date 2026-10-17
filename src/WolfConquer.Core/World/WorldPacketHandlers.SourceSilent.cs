using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2196)]
    private static ValueTask HandleLegacySourceSilent2196Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2038)]
    private static ValueTask HandleLegacySourceSilent2038Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2029)]
    private static ValueTask HandleLegacySourceSilent2029Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2145)]
    private static ValueTask HandleLegacySourceSilent2145Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2163)]
    private static ValueTask HandleLegacySourceSilent2163Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2234)]
    private static ValueTask HandleLegacySourceSilent2234Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyAchievement)]
    private static ValueTask HandleLegacySourceSilent2307Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2311)]
    private static ValueTask HandleLegacySourceSilent2311Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2369)]
    private static ValueTask HandleLegacySourceSilent2369Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2357)]
    private static ValueTask HandleLegacySourceSilent2357Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2382)]
    private static async ValueTask HandleLegacySourceSilent2382Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (context.ProtocolProfile == ConquerProtocolProfile.VisibleDx8D13)
        {
            await HandleVisibleDx8D13InstanceInfoAsync(context, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacySourceSilentAsync(context, frame, cancellationToken).ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2383)]
    private static ValueTask HandleLegacySourceSilent2383Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2417)]
    private static ValueTask HandleLegacySourceSilent2417Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2424)]
    private static ValueTask HandleLegacySourceSilent2424Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2433)]
    private static ValueTask HandleLegacySourceSilent2433Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2456)]
    private static ValueTask HandleLegacySourceSilent2456Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    [PacketHandler(WorldPacketOpcodes.LegacySourceSilent2469)]
    private static ValueTask HandleLegacySourceSilent2469Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        return HandleLegacySourceSilentAsync(context, frame, cancellationToken);
    }

    private static async ValueTask HandleLegacySourceSilentAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacySourceSilentPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed source-silent legacy packet {FormatSession(context)} opcode={frame.Header.Opcode}.")
                .ConfigureAwait(false);
            return;
        }

        var sourceName = packet.Info.SourceName ?? "unmapped";
        await context.Log.WriteLineAsync(
                "WorldServer source-silent legacy packet " +
                $"{FormatSession(context)} opcode={packet.Opcode} payloadLength={packet.Payload.Length}. " +
                $"sourceName={sourceName} sourceEvidence={packet.Info.SourceEvidence}. " +
                $"PARITY-GAP source-silent payload {FormatFrameDiagnostic(frame)}")
            .ConfigureAwait(false);
    }

}
