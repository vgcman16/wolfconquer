using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    [PacketHandler(WorldPacketOpcodes.LegacyAttributeSet)]
    private static async ValueTask HandleLegacyAttributeSetAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyAttributeSetPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyAttributeSet packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyAttributeSet {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyOwnedAttributeSetProposal.TryCreate(session.Character, packet, out var proposal) || proposal is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyAttributeSet " +
                    $"{FormatSession(context)} reason=invalid-attribute-allocation.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } store ||
            !store.TryCommitAttributeSet(context, session, proposal, out var receipt, out var rebuild, out var frames) ||
            receipt is null || rebuild is null)
        {
            await context.Log.WriteLineAsync("WorldServer refused LegacyAttributeSet: owned allocation or query preflight unavailable.")
                .ConfigureAwait(false);
            return;
        }
        try
        {
            if (!receipt.TryCurrent(out _)) throw new InvalidOperationException("ownedAttributeSetEchoOwnerChanged");
            await context.Connection.SendAsync(frames[0], cancellationToken).ConfigureAwait(false);
            if (!await rebuild.RebuildAsync(receipt, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("ownedAttributeSetCommittedQueryUnproven");
        }
        catch (Exception error)
        {
            receipt.FenceCommittedAttributeFailure(error);
            throw;
        }
        await context.Log.WriteLineAsync("WorldServer handled LegacyAttributeSet " +
            $"{FormatSession(context)} strength={packet.Strength} agility={packet.Agility} " +
            $"vitality={packet.Vitality} spirit={packet.Spirit}.")
            .ConfigureAwait(false);
    }

}
