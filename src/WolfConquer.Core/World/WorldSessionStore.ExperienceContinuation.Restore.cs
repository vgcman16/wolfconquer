using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Unwired until real query-only receipt, closed marker fences, recovery and
    // send-only continuation output are joined. Exactly one Character image
    // atomically contains both the real HP restoration and Ready marker.
    internal bool TryRestoreExperienceHitpoints(WorldPacketHandlerContext context,
        WorldPacketHandlers.CompletedContinuationQuery query, out OwnedContinuationCheckpoint? checkpoint, out string reason)
    {
        checkpoint = null; reason = "experienceContinuationRestoreUnavailable";
        RefuseBoundSpellReentry(context.Connection);
        if (!query.TryCurrent(out var current) || !ReferenceEquals(query.Session, current) ||
            current.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(binding.Connection, context.Connection) || framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, current, out var actual) ||
            actual.TradeOperations?.OwnsActiveExclusiveLease(actual.FrameOperationLease) != true) return false;
        WorldSession final = null!; object identity = null!; ActionPublication publication = null!;
        LegacyExperienceContinuation ready = null!; byte[] committedImage = null!;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, current);
            if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                if (!_sessions.TryGetValue(binding.Connection, out var holder) || !BindingIsCurrentPlain(binding, holder, current) ||
                    !frame.IsActionLifecycleCurrent(this, actual) || !binding.Recovery.IsReadyForUsers(binding.UsersRoot) ||
                    !binding.Recovery.TryRequireLiveJournalClear(out _)) return false;
                string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                string failure = "experienceContinuationRestoreImageInvalid";
                bool saved = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                {
                    byte[] before, after; WorldSession proposed; LegacyNativePlayerRuntime.OwnedHitpointsPublication vitals;
                    LegacyNativeAttributeMapProof oldAttributes, oldPaid, newAttributes, newPaid;
                    try
                    {
                        if (!query.TryCurrent(out var stillCurrent) || !ReferenceEquals(stillCurrent, current) ||
                            !CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationRestore", out before, out _)) return false;
                        oldAttributes = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
                        oldPaid = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.PaidNobility);
                        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(before, current.AccountId, checked((uint)current.Character.Uid));
                        if (pending is null || pending.Phase != LegacyExperienceContinuationPhase.RestorePending ||
                            pending.RestoreMaximum != query.MaximumHitpoints || !query.CoversPending(pending) ||
                            !Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId))) return false;
                        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
                        if (!LegacyCombatCharacterImageInputs.Matches(disk, current.Character)) return false;
                        ready = pending.MarkReady();
                        var character = LegacyEquipmentQueryBounds.WithHitpoints(current.Character, checked((int)query.MaximumHitpoints));
                        character = LegacyExperienceContinuationCodec.WithState(character, current.AccountId, ready);
                        character = new WorldPacketHandlers.BoundHitpointsInputs(current.Character, character).After;
                        proposed = current with { Character = character };
                        newAttributes = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.Attributes);
                        newPaid = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.PaidNobility);
                        if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, current, proposed)) return false;
                        vitals = LegacyNativePlayerRuntime.OwnedHitpointsPublication.PrepareBound(binding, current, proposed);
                        var scalars = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(before, character);
                        after = LegacyExperienceContinuationCodec.ProjectCharacterImage(before, scalars,
                            current.AccountId, checked((uint)current.Character.Uid), pending, ready);
                        publication = new ActionPublication(this, actual, frame, holder.NativeClientIdentity, binding, proposed,
                            LegacyActionState.RequireCurrent(proposed.Character));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                        InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    { return false; }
                    if (!oldAttributes.IsCurrentPlain() || !oldPaid.IsCurrentPlain() || !newAttributes.IsCurrentPlain() || !newPaid.IsCurrentPlain() ||
                        !query.TryCurrent(out var verified) || !ReferenceEquals(verified, current) || !vitals.IsCurrentPlain() ||
                        !frame.IsActionLifecycleCurrent(this, actual)) return false;
                    bool written; bool uncertain; string detail;
                    try
                    {
                        var permit = IssueContinuationFilePermit(this, actual, binding, current, path, before, after, query: query);
                        written = CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, after, permit, out uncertain, out detail);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    { written = false; uncertain = true; detail = error.GetType().Name; }
                    if (!written)
                    {
                        if (!uncertain) { failure = "experienceContinuationRestoreReplace:" + detail; return false; }
                        FenceContinuationOutcome(holder, binding, new InvalidOperationException("experienceContinuationRestoreOutcomeUnproven:" + detail));
                        throw new InvalidOperationException("experienceContinuationRestoreOutcomeUnproven");
                    }
                    try
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationRestoreCommitted", out var observed, out _) ||
                            !observed.AsSpan().SequenceEqual(after) || !query.TryCurrent(out var postWriteCurrent) ||
                            !ReferenceEquals(postWriteCurrent, current) || !oldAttributes.IsCurrentPlain() || !oldPaid.IsCurrentPlain() ||
                            !newAttributes.IsCurrentPlain() || !newPaid.IsCurrentPlain() || !vitals.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, actual))
                            throw new InvalidOperationException("experienceContinuationRestoreOwnerChanged");
                        vitals.PublishPrepared();
                        identity = holder.NativeClientIdentity; final = proposed;
                        holder.PublishPrepared(final, identity); publication.ConfirmCommitted(); committedImage = after.ToArray();
                        RecordCommittedContinuationRetirement(actual, binding, current, final, after);
                    }
                    catch (Exception error) { FenceContinuationOutcome(holder, binding, error); throw; }
                    return true;
                });
                if (!saved) { reason = failure; return false; }
            }
        }
        try { NotifyPublishedSessionChange(binding.Connection, current, final, identity, binding); }
        catch (Exception error) { publication.FenceCommittedLevelUpFailure(error); throw; }
        checkpoint = IssueContinuationCheckpoint(query, binding, actual, current, final, ready, committedImage, publication);
        reason = string.Empty; return true;
    }
}
