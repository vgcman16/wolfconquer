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
    [PacketHandler(WorldPacketOpcodes.LegacySpell)]
    private static async ValueTask HandleLegacySpellAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacySpellPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacySpell packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacySpell {FormatSession(context)} spellId={packet.Id}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var spells = session.PlayerSpells.Spells.ToArray();
        var spellIndex = -1;
        for (var index = 0; index < spells.Length; index++)
        {
            if (spells[index].Id == packet.Id)
            {
                spellIndex = index;
                break;
            }
        }

        if (spellIndex < 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacySpell soul selection " +
                    $"{FormatSession(context)} spellId={packet.Id}; spell is not learned.")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacySpellPacket.TryCreateSoulSelectionResponse(
                spells[spellIndex],
                packet,
                out var updatedSpell,
                out var response))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacySpell soul selection " +
                    $"{FormatSession(context)} spellId={packet.Id} useSoul={packet.UseSoul}; soul level is not learned.")
                .ConfigureAwait(false);
            return;
        }

        spells[spellIndex] = updatedSpell;
        var updatedSpells = new PlayerSpellSnapshot(spells);
        var updatedSession = session with { PlayerSpells = updatedSpells };
        var responseFrame = response.ToFrameBytes();

        if (!TryCommitLegacyFreeSpells(context, session, updatedSpells, cancellationToken,
                out updatedSession, out var reason))
        {
            await context.Log.WriteLineAsync("WorldServer refused soul selection before commitment; spell unchanged. reason=" + reason)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(responseFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacySpell soul selection " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={packet.Id} " +
                $"useSoul={packet.UseSoul} selectedSoulLevel={updatedSpell.UseSpellSoul}.")
            .ConfigureAwait(false);
    }

}
