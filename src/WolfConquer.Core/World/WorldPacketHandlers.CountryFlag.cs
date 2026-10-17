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

    [PacketHandler(WorldPacketOpcodes.LegacyCountryFlag)]
    private static async ValueTask HandleLegacyCountryFlagAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyCountryFlagPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyCountryFlag packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyCountryFlag {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithCharacterAttribute(session.Character, "CountryID", packet.CountryId);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var response = new LegacyCountryFlagPacket(
            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
            packet.CountryId);
        await SendCountryFlagVisibleAsync(context, updatedSession, response, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyCountryFlag {FormatSession(context)} countryId={packet.CountryId}.")
            .ConfigureAwait(false);
    }



    private static async ValueTask SendCountryFlagVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyCountryFlagPacket packet,
        CancellationToken cancellationToken)
    {
        var frame = packet.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

        var sentCount = 1;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == session.Character.Uid ||
                    !CanSeePlayer(active.Session.Character, session.Character))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sentCount++;
            }
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyCountryFlag " +
                $"{FormatSession(context)} uid={packet.Uid} countryId={packet.CountryId} playerCount={sentCount}.")
            .ConfigureAwait(false);
    }

}
