using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitWindWalkerQueryPostlude(WorldPacketHandlerContext context, WorldSession expected,
        LegacyEquipmentQueryPublicationScope scope, LegacyNativeMagicQueryClient.WindWalkerQuery query,
        out WorldSession? published, out WindWalkerPublication? notification) =>
        TryCommitWindWalker(context, expected, scope.OwnerNativeClientIdentity!, query, scope, out published, out notification);

    internal bool TryExpireWindWalkerFan(WorldPacketHandlerContext context, WorldSession expected, object identity,
        out WorldSession? published, out WindWalkerPublication? notification) =>
        TryCommitWindWalker(context, expected, identity, null, null, out published, out notification);

    private bool TryCommitWindWalker(WorldPacketHandlerContext context, WorldSession expected, object identity,
        LegacyNativeMagicQueryClient.WindWalkerQuery? query, LegacyEquipmentQueryPublicationScope? scope,
        out WorldSession? published, out WindWalkerPublication? notification)
    {
        published = null; notification = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame) return false;
        if (!frame.TryPrepareWindWalkerContext(this, context, expected, out var ownedContext)) return false;
        context = ownedContext;
        WorldSession? written = null; WindWalkerPublication? prepared = null; LegacyOrdinarySpellBinding? issuedBinding = null;
        lock (frame.RequireWindWalkerLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) || holder.SpellBinding is not { } binding ||
                    !BindingIsCurrentPlain(binding, holder, expected) || !frame.IsWindWalkerContext(this, context, expected)) return false;
                using var actors = new SpellActorScope(binding);
                if (binding.Player.BitVector is not { } vector) return false;
                lock (vector.SyncRoot)
                {
                    // These aliases must be valid even if the physical action
                    // would be a duplicate/no-op; never erase unrelated input.
                    var beforeWords = LegacyWindWalkerStatusProjection.Read(expected.Character);
                    if (!vector.TryCaptureWindWalker(out var before) || before is null) return false;
                    LegacyNativeStatusFlagsBigVector32.WindWalkerChange? change;
                    if (query is not null)
                    {
                        if (!LegacyNativeMagicQueryClient.TryValidateWindWalker(query, this, context.Connection, identity, expected, out var desired) ||
                            !vector.TryPrepareWindWalkerQuery(desired, out change)) return false;
                    }
                    else
                    {
                        if (!WindWalkerOwners.TryGetValue(binding.Player, out var oldSlot) || oldSlot.Owner is not { } joined ||
                            !WindWalkerBindingCurrentPlain(joined, binding.Player)) return false;
                        if (!before.Present || DateTime.Now < before.Timer.AddSeconds(before.Seconds))
                        { published = expected; return true; }
                        if (!vector.TryPrepareWindWalkerExpiry(out change)) return false;
                    }
                    if (change is null) return false;
                    var projected = LegacyWindWalkerStatusProjection.Apply(expected.Character, change.Proposed);
                    var inputs = new WorldPacketHandlers.WindWalkerStatusInputs(expected, change.Proposed);
                    var proposed = ReferenceEquals(projected, expected.Character) ? expected : expected with { Character = inputs.Character };
                    if (CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character, proposed.Character) ||
                        !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                    bool visibleChange = change.Changed || ((beforeWords[LegacyWindWalkerStatusProjection.Word] &
                        LegacyWindWalkerStatusProjection.Mask) != 0) != change.Proposed.Present;
                    // Preallocate an inert weak slot and its private marker.
                    // Only the final plain assignment installs live authority.
                    var slot = WindWalkerOwners.GetValue(binding.Player, static _ => new WindWalkerOwnerSlot());
                    var joinedAfter = slot.Owner ?? (before.Present || change.Proposed.Present
                        ? new JoinedWindWalker(binding, vector) : null);
                    prepared = WindWalkerPublication.Prepare(this, context, identity, binding, vector, change.Proposed,
                        proposed, visibleChange);
                    var proof = WindWalkerPublicationProof.Capture(this, context, expected, identity, change, query, scope, inputs, proposed, prepared);
                    if (!vector.TryCommitWindWalker(change, proof)) return false;
                    // Native COW and holder publication are one non-callback
                    // continuation. Actual persistence/observers run afterward.
                    slot.Owner = joinedAfter;
                    holder.PublishPrepared(proposed, identity);
                    try { prepared.ConfirmCommitted(proof); }
                    catch (Exception cause)
                    {
                        var failure = new WorldSessionPublicationException(this, context.Connection, identity, binding,
                            proposed, WorldSessionPublicationStage.ProcContinuation, cause);
                        BlockPublishedRecovery(failure);
                        try { FencePublishedOwnerUnderGate(failure); }
                        catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
                        throw failure;
                    }
                    if (!change.Changed && ReferenceEquals(proposed, expected))
                    { published = expected; notification = prepared; return true; }
                    written = proposed; issuedBinding = binding;
                    PersistFrameOwnedSessionChange(context.Connection, expected, proposed, identity, binding);
                }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, written!, identity, issuedBinding);
        published = written; notification = prepared; return true;
    }
}
