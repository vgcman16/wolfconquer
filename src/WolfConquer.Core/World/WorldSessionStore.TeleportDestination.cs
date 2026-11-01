namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsTeleportDestinationWriteOwnerCurrent(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldPacketHandlers.OwnedTeleportDestinationProjection plan)
    {
        if (!plan.IsCurrentPlain() || !IsMovementWriteOwnerCurrent(frame, context, binding, plan.Expected) ||
            !_sessions.TryGetValue(binding.Connection, out var holder)) return false;
        var original = plan.OriginalOwner;
        return ReferenceEquals(original, context.TeleportOwner) && (original is null ||
            ReferenceEquals(original.World, this) && ReferenceEquals(original.Connection, binding.Connection) &&
            original.Uid == binding.Uid && ReferenceEquals(original.Identity, holder.NativeClientIdentity));
    }

    internal bool TryCommitOwnedTeleportDestination(WorldPacketHandlerContext context,
        WorldPacketHandlers.OwnedTeleportDestinationProjection plan, out WorldSession? final)
    {
        final = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, plan.Expected, out var actual)) return false;
        context = actual;
        object identity; LegacyOrdinarySpellBinding binding;
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
                LegacyPreparedTeleportDestinationWrite writer;
                try { writer = LegacyPreparedTeleportDestinationWrite.Prepare(frame, this, context, binding, plan); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                    ArgumentException or OverflowException or FormatException) { return false; }
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, plan.Expected,
                    writer.Proposed, teleportPublication: writer.PreparePublication());
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, binding, plan.Expected);
                var canonical = publication.FinalSession;
                final = canonical; identity = publication.PublishedIdentity;
                publication.ArmFinalProof();
                if (!plan.IsCurrentPlain() || !cancellation.IsCurrentPlain() || !publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
                var outcome = writer.TryCommit(out var reason);
                if (outcome == LegacyPreparedTeleportDestinationWrite.Outcome.Rejected) { final = null; return false; }
                if (outcome == LegacyPreparedTeleportDestinationWrite.Outcome.RecoveryRequired)
                    throw FenceTeleportDestination(binding, "teleportDestinationWriteUnproven:" + reason);
                if (!publication.AcceptExternallyPersistedHitpoints() || !publication.IsCurrentPlain() || !cancellation.IsCurrentPlain())
                    throw FenceTeleportDestination(binding, "teleportDestinationCommittedPublicationUnproven");
                publication.PublishNative(); cancellation.PublishPrepared(); publication.PublishHolder();
                publication.Dispose();
                try { PersistFrameOwnedSessionChange(context.Connection, plan.Expected, canonical, identity, binding, publication.HitpointsWrite); }
                catch (Exception error) { throw FenceTeleportDestination(binding, "teleportDestinationCommittedPostludeFailed", error); }
            }
        }
        var published = final ?? throw InvalidSpellPublication();
        try { NotifyPublishedSessionChange(context.Connection, plan.Expected, published, identity, binding); }
        catch (Exception error) { throw FenceTeleportDestination(binding, "teleportDestinationCommittedObserverFailed", error); }
        return true;
    }

    private Exception FenceTeleportDestination(LegacyOrdinarySpellBinding binding, string reason, Exception? cause = null)
    {
        var failure = new LegacyNativeHitpointsRecoveryRequiredException(reason);
        var errors = new List<Exception> { cause is null ? failure : new AggregateException(reason, failure, cause) };
        try { binding.Recovery.Block(reason); } catch (Exception error) { errors.Add(error); }
        try
        {
            if (_connectionGates.TryGetValue(binding.Connection, out var gate))
            {
                lock (gate)
                {
                    using var nativeGate = EnterNativeConnectionGate();
                    if (_sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.SpellBinding, binding))
                        holder.Clear(); // Actual retirement consumes its closed native repeat-cancel proof.
                    else binding.Retire();
                }
            }
            else binding.Retire();
        }
        catch (Exception error) { errors.Add(error); }
        return errors.Count == 1 ? errors[0] : new AggregateException(reason, errors);
    }
}
