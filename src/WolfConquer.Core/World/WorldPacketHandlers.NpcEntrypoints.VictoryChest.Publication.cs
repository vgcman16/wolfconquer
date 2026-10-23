namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishVictoryChestAsync(VictoryChestPlan plan, CancellationToken cancellationToken)
    {
        var owner = plan.Before.Binding;
        var context = owner.Context;
        try
        {
            if (!LegacyVictoryChestPersistence.IsCurrent(owner, plan.Updated)) return;
            await context.Log.WriteLineAsync(plan.Persisted).ConfigureAwait(false);
            foreach (var frame in plan.Frames)
            {
                if (!LegacyVictoryChestPersistence.IsCurrent(owner, plan.Updated)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            if (LegacyVictoryChestPersistence.IsCurrent(owner, plan.Updated))
                await context.Log.WriteLineAsync(plan.Success).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // All durable images and memory already committed. A delivery/log
            // failure is not an unchanged rejection, claim refund or retry permit.
            // Do not recurse through a failed logger or claim an outbox/delivery.
        }
    }
}
