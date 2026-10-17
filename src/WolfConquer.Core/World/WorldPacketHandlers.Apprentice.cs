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
    [PacketHandler(WorldPacketOpcodes.LegacyApprenticeInformation)]
    private static async ValueTask HandleLegacyApprenticeInformationAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (frame.Payload.Length == 0)
        {
            await HandleLegacyApprenticeInformationQueryAsync(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!LegacyApprenticeInformationPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyApprenticeInformation packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer source-backed no-op LegacyApprenticeInformation " +
                $"{FormatSession(context)} mode={packet.Mode} mentorId={packet.MentorId} apprenticeId={packet.ApprenticeId}; " +
                "source MsgApprenticeInformation defines response payload serialization but no inbound request processing in legacy; no state mutation is modeled.")
                .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyApprenticeInformationQueryAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyApprenticeInformation " +
                    $"{FormatSession(context)} payloadLength=0; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyApprenticeInformation " +
                    $"{FormatSession(context)} payloadLength=0; social association data is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid < int.MinValue || session.Character.Uid > int.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyApprenticeInformation " +
                    $"{FormatSession(context)} payloadLength=0; character uid is outside the social association key range.")
                .ConfigureAwait(false);
            return;
        }

        var ownerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        worldData.SocialAssociationsByOwnerId.TryGetValue((int)session.Character.Uid, out var records);
        records ??= [];

        var onlineTargetIds = context.Sessions?
            .Snapshot()
            .Where(active => active.Connection != context.Connection)
            .Select(active => CheckedUInt32(active.Session.Character.Uid, nameof(active.Session.Character.Uid)))
            .ToHashSet();
        var responses = LegacyApprenticeInformationStartupPackets.CreateOfflineApprenticeStartup(
            records,
            ownerUid,
            onlineTargetIds);

        foreach (var response in responses)
        {
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyApprenticeInformation " +
                $"{FormatSession(context)} payloadLength=0 responseFrames={responses.Count}; " +
                "source=MsgApprenticeInformation/Associate offline apprentice startup.")
            .ConfigureAwait(false);
    }

}
