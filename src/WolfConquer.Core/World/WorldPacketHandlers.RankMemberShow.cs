using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyRankMemberShow)]
    private static async ValueTask HandleLegacyRankMemberShowAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyRankMemberShowPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyRankMemberShow packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRankMemberShow " +
                    $"{FormatSession(context)} type={query.Type}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var requestedType = query.Items.Count > 0 ? query.Items[0].Type : 0;
        var isPrestigeChampionQuery = query.Type == LegacyRankMemberShowModeShow &&
            requestedType == LegacyRankMemberShowPrestigeChampionType;
        byte[] responseBytes;
        int responseItemCount;
        if (isPrestigeChampionQuery)
        {
            if (!HasCurrentLegacyPrestigeQuerySession(context))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyRankMemberShow " +
                        $"{FormatSession(context)} type={query.Type} requestedType={requestedType}; current connection session is unavailable.")
                    .ConfigureAwait(false);
                return;
            }

            if (!TryPrepareLegacyPrestigeChampionResponse(
                    context.WorldData,
                    out responseBytes,
                    out responseItemCount,
                    out var rejection))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyRankMemberShow " +
                        $"{FormatSession(context)} type={query.Type} requestedType={requestedType}; {rejection}.")
                    .ConfigureAwait(false);
                return;
            }
        }
        else
        {
            var response = LegacyRankMemberShowPacket.EmptyResponse(query.Type);
            responseBytes = response.ToFrameBytes();
            responseItemCount = response.Items.Count;
        }

        await context.Connection.SendAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        var detail = isPrestigeChampionQuery
            ? "validated prestige snapshot"
            : "retained empty compatibility response; requested family is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyRankMemberShow " +
                $"{FormatSession(context)} type={query.Type} requestedItems={query.Items.Count} " +
                $"requestedType={requestedType} responseItems={responseItemCount}; {detail}.")
            .ConfigureAwait(false);
    }
}
