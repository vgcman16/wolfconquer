namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyViewEquipmentPrestigeCounts(
        int AbilityScorePacketCount,
        int AbilityScoreItemCount);

    private readonly record struct LegacyViewEquipmentItemCounts(
        int ItemViewPacketCount,
        int ItemExtraPacketCount);

    private readonly record struct LegacyViewEquipmentRuneCounts(int RuneViewPacketCount);

    private readonly record struct LegacyViewEquipmentProfileCounts(
        int ViewEquipSpousePacketCount,
        int StatusPacketCount);

    private static async ValueTask HandleViewEquipmentActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } requesterSession)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-silent no-op MsgAction ViewEquipment; no requester session.")
                .ConfigureAwait(false);
            return;
        }

        var targetUid = packet.DwParam;
        if (!TryGetSessionByUid(context, targetUid, out var target))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-silent no-op MsgAction ViewEquipment " +
                    $"{FormatSession(context)} targetUid={targetUid}; target is not an active player.")
                .ConfigureAwait(false);
            return;
        }

        var targetSession = target.Session;
        if (targetSession.PlayerItems is not { EquipmentItems: not null })
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-silent no-op MsgAction ViewEquipment " +
                    $"{FormatSession(context)} targetUid={targetUid}; target equipment snapshot is null.")
                .ConfigureAwait(false);
            return;
        }

        var prestigeCounts = await SendLegacyViewEquipmentPrestigeAsync(
                context,
                targetSession,
                cancellationToken)
            .ConfigureAwait(false);
        var itemCounts = await SendLegacyViewEquipmentItemsAsync(
                context,
                targetSession,
                cancellationToken)
            .ConfigureAwait(false);
        var runeCounts = await SendLegacyViewEquipmentRunesAsync(
                context,
                targetSession,
                cancellationToken)
            .ConfigureAwait(false);
        var profileCounts = await SendLegacyViewEquipmentProfileAsync(
                context,
                requesterSession,
                targetSession,
                cancellationToken)
            .ConfigureAwait(false);
        var targetNoticeSent = await TrySendViewEquipmentNoticeAsync(
                requesterSession,
                target,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled source-backed MsgAction ViewEquipment " +
                $"{FormatSession(context)} targetUid={targetUid} " +
                $"abilityScorePackets={prestigeCounts.AbilityScorePacketCount} " +
                $"abilityScoreItems={prestigeCounts.AbilityScoreItemCount} " +
                $"itemViewPackets={itemCounts.ItemViewPacketCount} " +
                $"itemExtraPackets={itemCounts.ItemExtraPacketCount} " +
                $"runeViewPackets={runeCounts.RuneViewPacketCount} " +
                $"viewEquipSpousePackets={profileCounts.ViewEquipSpousePacketCount} " +
                $"statusPackets={profileCounts.StatusPacketCount} " +
                $"targetNoticeSent={(targetNoticeSent ? 1 : 0)}; " +
                "source=MsgDataPacket.ViewEquipment [Data(117)].")
            .ConfigureAwait(false);
    }

}
