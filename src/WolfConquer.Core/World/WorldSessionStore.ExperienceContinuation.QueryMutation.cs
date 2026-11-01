using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Only the actual private equipment-query execution can register this
    // exact one-use proposal. No projection delegate enters the Store.
    internal bool TryCommitContinuationQueryMutation(WorldPacketHandlers.OwnedContinuationQueryMutation proposal,
        out WorldSession? final)
    {
        final = null;
        var context = proposal.Context; var expected = proposal.Expected; var proposed = proposal.Proposed;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || !frame.TryPrepareActionContext(this, context, expected, out var actual) ||
            !ReferenceEquals(actual, context) || context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true) return false;
        WorldSession written = null!; object identity = null!; LegacyOrdinarySpellBinding binding = null!;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } owner || !BindingIsCurrentPlain(owner, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) || !owner.Recovery.IsReadyForUsers(owner.UsersRoot) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out _) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var actors = new SpellActorScope(owner);
                {
                    if (!proposal.IsCurrentPlain(owner)) return false;
                    proposal.ArmFinalProof(owner);
                    using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed,
                        continuationQuery: proposal);
                    if (publication.HitpointsWrite is null &&
                        CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character, publication.FinalSession.Character))
                        return false;
                    if (!TryPublishFrameOwnedHitpoints(publication, holder, out _, continuationQuery: proposal)) return false;
                    written = publication.FinalSession; identity = publication.PublishedIdentity; binding = owner;
                    publication.Dispose();
                    PersistFrameOwnedSessionChange(context.Connection, expected, written, identity, binding, publication.HitpointsWrite);
                    RecordCommittedContinuationRetirement(context, binding, expected, written, publication.HitpointsWrite?.CopyAfterImage());
                }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, written, identity, binding);
        final = written; return true;
    }

    private static bool SameContinuationQuerySession(WorldSession proposed, WorldSession final)
    {
        try
        {
            var first = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.Attributes);
            var second = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes);
            var firstPaid = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.PaidNobility);
            var secondPaid = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.PaidNobility);
            return (proposed with { Character = final.Character }) == final &&
                (proposed.Character with { Attributes = final.Character.Attributes, PaidNobility = final.Character.PaidNobility }) == final.Character &&
                ReferenceEquals(first.CapturedComparer, second.CapturedComparer) && first.Fields.SequenceEqual(second.Fields) &&
                ReferenceEquals(firstPaid.CapturedComparer, secondPaid.CapturedComparer) && firstPaid.Fields.SequenceEqual(secondPaid.Fields) &&
                first.IsCurrentPlain() && second.IsCurrentPlain() && firstPaid.IsCurrentPlain() && secondPaid.IsCurrentPlain();
        }
        catch (InvalidOperationException) { return false; }
    }
}
