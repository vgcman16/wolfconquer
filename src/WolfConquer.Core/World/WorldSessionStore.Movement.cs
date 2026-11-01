namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsMovementWriteOwnerCurrent(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected) =>
        ReferenceEquals(framePersistenceOwner, frame) && frame.IsActionLifecycleCurrent(this, context) &&
        ReferenceEquals(context.Connection, binding.Connection) && ReferenceEquals(binding.Store, this) &&
        _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && _sessions.TryGetValue(binding.Connection, out var holder) &&
        ReferenceEquals(holder.SpellBinding, binding) && BindingIsCurrentPlain(binding, holder, expected);

    internal bool TryCommitOwnedMovement(WorldPacketHandlerContext context, WorldPacketHandlers.OwnedMovementProjection plan,
        out ActionPublication? receipt)
    {
        receipt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, plan.Expected, out var actual)) return false;
        context = actual;
        WorldSession final; LegacyOrdinarySpellBinding binding; object identity; ActionPublication prepared;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, plan.Expected, plan.Proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), plan.Expected) ||
                    holder.SpellBinding is not { } current || !BindingIsCurrentPlain(current, holder, plan.Expected) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, plan.Expected, plan.Proposed)) return false;
                binding = current;
                using var actors = new SpellActorScope(binding);
                var writer = LegacyPreparedMovementWrite.Prepare(frame, this, context, binding, plan);
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, plan.Expected,
                    writer.Proposed, movementPublication: writer.PreparePublication());
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, binding, plan.Expected);
                final = publication.FinalSession; identity = publication.PublishedIdentity;
                prepared = new(this, context, frame, identity, binding, final, plan.Action, plan);
                publication.ArmFinalProof();
                if (!plan.IsCurrentPlain() || !cancellation.IsCurrentPlain() || !publication.ConsumeBeforeWriterPlain())
                    throw InvalidSpellPublication();
                var outcome = writer.TryCommit(out var reason);
                if (outcome == LegacyPreparedMovementWrite.Outcome.Rejected) return false;
                if (outcome == LegacyPreparedMovementWrite.Outcome.RecoveryRequired)
                {
                    var failure = new LegacyNativeHitpointsRecoveryRequiredException("ordinaryMovementRecoveryRequired:" + reason);
                    try { binding.Recovery.Block(failure.Message); }
                    catch (Exception fence) { throw new AggregateException("Movement persistence and fencing failed.", failure, fence); }
                    finally { holder.Clear(); }
                    throw failure;
                }
                if (!publication.AcceptExternallyPersistedHitpoints() || !publication.IsCurrentPlain() || !cancellation.IsCurrentPlain())
                    FailCommittedSpellPublication(holder, binding);
                publication.PublishNative(); cancellation.PublishPrepared(); publication.PublishHolder();
                prepared.ConfirmCommitted();
                publication.Dispose();
                PersistFrameOwnedSessionChange(context.Connection, plan.Expected, final, identity, binding, publication.HitpointsWrite);
            }
        }
        NotifyPublishedSessionChange(context.Connection, plan.Expected, final, identity, binding);
        receipt = prepared;
        return true;
    }
}
