using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A receipt of this real per-target journal22 publication. It does not
    // authorize another target, another hit, or a fresh request.
    internal sealed class OwnedDragonhowlDeathReceipt
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlDeathReceipt(WorldSessionStore store) => this.store = store;

        internal bool CoversCommittedDeathSpells(WorldServerFrameHandler frame, WorldSessionStore issuingStore,
            ConquerTcpConnection connection, WorldSession? before, WorldSession after) =>
            ReferenceEquals(store, issuingStore) && store.dragonhowlDeathReceipts.TryGetValue(this, out var data) &&
            ReferenceEquals(data.Owner.Frame, frame) && ReferenceEquals(data.Purpose.TargetBinding.Connection, connection) &&
            ReferenceEquals(data.Before, before) && ReferenceEquals(data.After, after) && data.IsPublishedCurrentPlain() &&
            data.Writer.MatchesActualCommittedAfterImages();

        internal bool TryCurrent()
        {
            if (!store.dragonhowlDeathReceipts.TryGetValue(this, out var data)) return false;
            lock (data.Owner.Frame.RequireActionLifecycleGate(store))
            {
                using var scope = new DragonhowlOwnerScope(store, DragonhowlCurrentRows(data.Owner));
                return data.IsPublishedCurrentPlain() && data.Writer.MatchesActualCommittedAfterImages();
            }
        }
    }

    private sealed class PreparedDragonhowlDeath : IDisposable
    {
        private readonly WorldSessionStore store;
        internal readonly DragonhowlOwnerState Owner;
        internal readonly OwnedDragonhowlDeath Purpose;
        internal readonly LegacyPreparedBasicPhysicalWrite Writer;
        internal readonly WorldSession Before, After;
        private readonly LegacyBoundHitpointsScalarWrite hitpoints;
        private readonly LegacyNativeMagicQueryClient.OwnedPhysicalDeathPublication query;
        private readonly LegacyNativeStatusFlagsBigVector32.OwnedPhysicalDeathWindWalkerPublication wind;
        private readonly PreparedBasicPhysicalChange cancellation;
        internal readonly WorldPacketHandlers.DragonhowlDeathNotifications Notifications;
        internal readonly OwnedDragonhowlDeathReceipt Receipt;
        private bool published;

        internal PreparedDragonhowlDeath(WorldSessionStore store, DragonhowlOwnerState owner,
            OwnedDragonhowlDeath purpose, LegacyPreparedBasicPhysicalWrite writer,
            PreparedSessionSpellPublication publication)
        {
            this.store = store; Owner = owner; Purpose = purpose; Writer = writer;
            Before = purpose.ExpectedTarget; After = publication.FinalSession;
            hitpoints = publication.HitpointsWrite ?? throw InvalidSpellPublication();
            query = LegacyNativeMagicQueryClient.OwnedPhysicalDeathPublication.Prepare(purpose.TargetBinding, purpose.Death, After);
            wind = LegacyNativeStatusFlagsBigVector32.OwnedPhysicalDeathWindWalkerPublication.Prepare(purpose.TargetBinding, purpose.Death, After);
            cancellation = store.PrepareBasicPhysicalCancellation(owner.Frame, owner.Context, purpose.TargetBinding, Before);
            Receipt = new(store);
            store.dragonhowlDeathReceipts.Add(Receipt, this); // Inert before all real participants publish.
            Notifications = purpose.Death.PrepareDragonhowlNotifications(purpose, Receipt, After);
            if (!IsCurrentPlain()) { Dispose(); throw InvalidSpellPublication(); }
        }

        internal bool IsCurrentPlain() => !published && Purpose.IsCurrentPlain() && Writer.IsCurrent() &&
            query.IsCurrentPlain() && wind.IsCurrentPlain() && cancellation.IsCurrentPlain() && Notifications.IsCurrentPlain();

        internal void PublishPrepared()
        {
            query.PublishPrepared(); wind.PublishPrepared(); Notifications.PublishPrepared(); cancellation.PublishPrepared();
            published = true;
        }

        internal bool IsPublishedCurrentPlain() => published && hitpoints.IsCommitted && !Owner.Failed &&
            store.DragonhowlHeldCurrent(Owner) && ReferenceEquals(Owner.Current[Purpose.TargetBinding.Connection], After) &&
            query.IsPublishedCurrentPlain() && wind.IsPublishedCurrentPlain() && Notifications.IsPublishedCurrentPlain() &&
            cancellation.After.IsCurrentPlain(Purpose.TargetBinding);

        public void Dispose() => Notifications.Dispose();
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedDragonhowlDeathReceipt, PreparedDragonhowlDeath>
        dragonhowlDeathReceipts = new();

    internal async ValueTask<bool> PublishDragonhowlDeathAsync(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        int index, CancellationToken token)
    {
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var owner) || !ReferenceEquals(owner.Plan, plan) ||
            !owner.Deaths.TryGetValue(index, out var death) || !death.Receipt.TryCurrent()) return false;
        return await death.Notifications.PublishAsync(token).ConfigureAwait(false);
    }
}
