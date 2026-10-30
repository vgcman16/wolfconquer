namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishVictoryChestAsync(VictoryChestPlan plan, WorldSession final, CancellationToken cancellationToken)
    {
        var owner = plan.Before.Binding;
        var context = owner.Context with { Session = final };
        try
        {
            if (!LegacyVictoryChestPersistence.IsCurrent(owner, final)) return;
            await context.Log.WriteLineAsync(plan.Persisted).ConfigureAwait(false);
            foreach (var frame in plan.Frames)
            {
                if (!LegacyVictoryChestPersistence.IsCurrent(owner, final)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            if (LegacyVictoryChestPersistence.IsCurrent(owner, final))
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
