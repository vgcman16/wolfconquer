namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishDeityChestAsync(WorldPacketHandlerContext context, DeityChestOwner owner,
        DeityChestPlan plan, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var frame in plan.Frames)
            {
                if (!IsCurrentDeityChestOwner(context, owner, plan.Updated)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            if (IsCurrentDeityChestOwner(context, owner, plan.Updated))
                await context.Log.WriteLineAsync(plan.Success).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Persistence + memory already committed. Delivery/logging failure
            // cannot make the payment retryable or restore a consumed key. Never
            // recurse through a failed logger or claim transport delivery.
        }
    }
}
