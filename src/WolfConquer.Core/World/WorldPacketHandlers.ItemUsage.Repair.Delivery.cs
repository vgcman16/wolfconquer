using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool ItemRepairDeliveryIsCurrent(
        WorldPacketHandlerContext context, ItemRepairOwner owner, WorldSession committed)
    {
        if (context.Trades is not { } trades || context.Sessions is not { } sessions)
            return false;
        lock (trades.SyncRoot)
        {
            if (!sessions.TryGet(context.Connection, out var current) || current is null ||
                !ReferenceEquals(current.PlayerItems, committed.PlayerItems) ||
                current.Character.Money != committed.Character.Money)
                return false;
            // Match writer order: actual package/Store/native actors before PIN.
            // Both scopes end before any network publication.
            using var preparation = sessions.PrepareItemRepair(context, current);
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
            {
                return ReferenceEquals(preparation.Identity, owner.Identity) &&
                    ReferenceEquals(preparation.Binding, owner.Binding) &&
                    ItemRepairOwnerIsCurrent(context, owner, current);
            }
        }
    }

    private static async ValueTask DeliverItemRepairAsync(
        WorldPacketHandlerContext context,
        ItemRepairOwner owner,
        ItemRepairPlan plan,
        WorldSession canonical,
        CancellationToken cancellationToken)
    {
        // All buffers were built before the real writer. Store's actual
        // canonical result is the delivery authority; preparation record
        // identity is not substituted for publication truth.
        try
        {
            if (canonical.Character.Money != plan.Updated.Character.Money ||
                !plan.After.IsCurrent(plan.Updated.PlayerItems) ||
                !plan.After.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(canonical.PlayerItems)))
                throw new InvalidOperationException("itemRepairCommittedDeliveryImageChanged");
        }
        catch (Exception primary)
        {
            Exception? secondary = null;
            FenceUncertainRepair(owner, "itemRepairRecoveryRequired:canonicalDeliveryImageUnproven", ref secondary);
            try { owner.Binding?.Retire(); }
            catch (Exception retire)
            {
                secondary = secondary is null ? retire :
                    new AggregateException("Item repair recovery and retirement failed.", secondary, retire);
            }
            if (secondary is not null)
                throw new AggregateException("Committed item repair delivery image is unproven.", primary, secondary);
            throw;
        }
        foreach (var frame in plan.Frames)
        {
            if (!ItemRepairDeliveryIsCurrent(context, owner, canonical))
                return;
            // No Exclusive, Trades, Store, native actor or file gate crosses
            // this transport await. A fault is a committed notification failure,
            // never a refund, writer retry or an unchanged-operation report.
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!ItemRepairDeliveryIsCurrent(context, owner, canonical))
                return;
        }
        var repairKind = plan.Payment == LegacyItemRepairPayment.Money ? "paid repair" : "zero-durability repair";
        await context.Log.WriteLineAsync(
            $"WorldServer handled MsgItemUsage {repairKind} {FormatSession(context with { Session = canonical })} " +
            $"itemUid={plan.TargetUid} payment={plan.Payment} cost={plan.Cost}; persistedBeforeReply=True.")
            .ConfigureAwait(false);
    }
}
