namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleAllowAnimationActionAsync(WorldPacketHandlerContext context,
        MsgActionPacket packet, CancellationToken cancellationToken)
    {
        if (context.Session is not { } session) return;
        var ownerUid = session.Character.Uid;
        var scope = new LegacyEquipmentQueryPublicationScope(ownerUid);
        var query = await ExecuteLegacyEquipmentQueryAsync(context, session,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.AllowAnimation, scope),
            cancellationToken).ConfigureAwait(false);
        if (!query.Completed) return;
        var current = RefreshLegacyEquipmentQueryPublication(context, query.Session!, scope);
        if (!current.Completed) return;
        await context.Connection.SendAsync((packet with
                { ObjId = CheckedUInt32(ownerUid, nameof(ownerUid)) }).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        // Native Data251 has no second query, gate or state write after this echo.
    }
}
