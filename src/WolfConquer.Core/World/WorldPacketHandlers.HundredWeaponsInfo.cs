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
    [PacketHandler(WorldPacketOpcodes.LegacyHundredWeaponsInfo)]
    private static async ValueTask HandleLegacyHundredWeaponsInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyHundredWeaponsInfoPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyHundredWeaponsInfo packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHundredWeaponsInfo " +
                    $"{FormatSession(context)} type={query.Type} ownerUid={query.OwnerUid}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        LegacyHundredWeaponsInfoPacket response;
        if (query.OwnerUid == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)))
        {
            response = CreateLegacyHundredWeaponsInfoResponse(query.Type, session.Character);
        }
        else if (TryGetSessionByUid(context, query.OwnerUid, out var target))
        {
            response = CreateLegacyHundredWeaponsInfoResponse(query.Type, target.Session.Character);
        }
        else
        {
            response = new LegacyHundredWeaponsInfoPacket(query.Type, OwnerUid: 0, Weapons: []);
        }

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyHundredWeaponsInfo " +
                $"{FormatSession(context)} type={query.Type} ownerUid={query.OwnerUid} " +
                $"responseOwnerUid={response.OwnerUid} weapons={response.Weapons.Count}.")
            .ConfigureAwait(false);
    }

    private static LegacyHundredWeaponsInfoPacket CreateLegacyHundredWeaponsInfoResponse(
        LegacyHundredWeaponsInfoAction type,
        CharacterSnapshot character)
    {
        return new LegacyHundredWeaponsInfoPacket(
            type,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            Weapons: []);
    }

}
