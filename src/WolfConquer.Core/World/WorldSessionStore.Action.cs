using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitStance(WorldPacketHandlerContext context, WorldPacketHandlers.StanceProjection plan,
        out ActionPublication? receipt)
    {
        receipt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, plan.Expected, out var actual)) return false;
        context = actual;
        WorldSession final; object identity; LegacyOrdinarySpellBinding binding; ActionPublication prepared;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, plan.Expected, plan.Proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), plan.Expected) ||
                    holder.SpellBinding is not { } currentBinding || !BindingIsCurrentPlain(currentBinding, holder, plan.Expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) || !plan.IsCurrentPlain() ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, plan.Expected, plan.Proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, plan.Expected, plan.Proposed);
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, currentBinding, plan.Expected);
                final = publication.FinalSession; identity = publication.PublishedIdentity; binding = currentBinding;
                // Action and ActivePick are transient. No defaulted scalar or
                // second Character write may be smuggled into a stance commit.
                if (publication.HitpointsWrite is not null ||
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(plan.Expected.Character, final.Character))
                    throw new InvalidOperationException("stanceProjectionChangedPersistedState");
                prepared = new(this, context, frame, identity, binding, final, plan.Action);
                if (!frame.IsActionLifecycleCurrent(this, context) || !plan.IsCurrentPlain() ||
                    !TryPublishFrameOwnedHitpoints(publication, holder, out _, basicPhysical: cancellation)) return false;
                prepared.ConfirmCommitted();
                publication.Dispose();
                PersistFrameOwnedSessionChange(context.Connection, plan.Expected, final, identity, binding);
            }
        }
        receipt = prepared;
        NotifyPublishedSessionChange(context.Connection, plan.Expected, final, identity, binding);
        return true;
    }
}
