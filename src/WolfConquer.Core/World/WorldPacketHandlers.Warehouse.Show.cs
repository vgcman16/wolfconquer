namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyWarehouseShowAsync(WorldPacketHandlerContext context,
        LegacyWarehousePacket request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCaptureLegacyWarehouseShowOwner(context, out var owner) ||
            !TryPrepareLegacyWarehouseShow(owner, request, out var frames, out var count))
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyWarehouse show {FormatSession(context)} " +
                $"npcId={request.NpcId} action={request.Action}; current owner, target or complete image unavailable.").ConfigureAwait(false);
            return;
        }
        await SendLegacyWarehouseShowAsync(context, owner, request, frames, count, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarehouseShowAsync(WorldPacketHandlerContext context,
        LegacyWarehouseShowOwner owner, LegacyWarehousePacket request, byte[][] frames, int count,
        CancellationToken cancellationToken, bool requireRefreshOwner = false)
    {
        var currentContext = context with { Session = owner.Session };
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentLegacyWarehouseShowOwner(context, owner) ||
                !CanShowLegacyWarehouse(owner, request) ||
                requireRefreshOwner && !CanPublishLegacyWarehouseLockRefresh(context, owner))
            {
                await context.Log.WriteLineAsync("WorldServer stopped LegacyWarehouse show; current owner or permission changed.")
                    .ConfigureAwait(false);
                return;
            }
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        if (request.Action == LegacyWarehouseAction.ShashShow)
        {
            await context.Log.WriteLineAsync($"WorldServer sent LegacyWarehouse sash show {FormatSession(currentContext)} " +
                $"sashUid={request.NpcId} count={count}.").ConfigureAwait(false);
        }
        else if (count != 0)
        {
            await context.Log.WriteLineAsync($"WorldServer sent LegacyWarehouse {FormatSession(currentContext)} " +
                $"npcId={request.NpcId} action={request.Action} count={count}.").ConfigureAwait(false);
        }
        else if (request.Action == LegacyWarehouseAction.ShowInventorySash)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyWarehouse inventory sash show {FormatSession(currentContext)} " +
                $"npcId={request.NpcId}; inventory sash is empty (loadedInventoryItems={owner.Session.PlayerItems.InventoryItems.Count} " +
                $"loadedWarehouseItems={owner.Session.PlayerItems.WarehouseItems.Count}).").ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyWarehouse show {FormatSession(currentContext)} " +
                $"npcId={request.NpcId}; no source items are loaded.").ConfigureAwait(false);
        }
    }
}
