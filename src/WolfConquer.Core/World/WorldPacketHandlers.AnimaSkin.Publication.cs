namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishDragonSkinMutationAsync(WorldPacketHandlerContext context,
        DragonSkinOwner owner, DragonSkinMutationPlan plan, WorldSession final, LegacyDragonSkinPacket packet,
        CancellationToken cancellationToken)
    {
        context = context with { Session = final };
        // The plan remains the original prewriter proof; only the actual Store
        // receipt is current-session authority for postcommit publication.
        // All serializers ran before the true two-file/session commit. Source
        // success order remains consume/add -> persistence log -> action log -> ack.
        if (!IsCurrentDragonSkinOwner(context, owner, final)) return;
        try
        {
            await context.Connection.SendAsync(plan.InventoryFrame, cancellationToken).ConfigureAwait(false);
            if (!IsCurrentDragonSkinOwner(context, owner, final)) return;
            await context.Log.WriteLineAsync($"WorldServer persisted LegacyDragonSkin {FormatSession(context with { Session = final })} " +
                $"path={final.AccountId}.ini keys=AnimaSkin; itemsPersisted=True.").ConfigureAwait(false);
            if (!IsCurrentDragonSkinOwner(context, owner, final)) return;
            var action = packet.Type == LegacyDragonSkinType.Unlock ? "unlocked" : "retrieved";
            await context.Log.WriteLineAsync($"WorldServer {action} LegacyDragonSkin {FormatSession(context with { Session = final })} " +
                $"skinType={packet.SkinType} itemId={plan.ItemId}.").ConfigureAwait(false);
            if (!IsCurrentDragonSkinOwner(context, owner, final)) return;
            await context.Connection.SendAsync(plan.Acknowledgement, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Commit is irrevocable here. A socket, cancellation, or its logging
            // observer cannot relabel it as rejected/retryable or restore skin/items.
            // Do not recurse through the same possibly broken logger.
        }
    }
}
