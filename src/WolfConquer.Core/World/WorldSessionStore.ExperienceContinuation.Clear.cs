using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ExperienceContinuationExecution
    {
        // Removes a completed marker only from this real current execution.
        // It does not send XP or authorize a subsequent award. The caller must
        // retain the real exclusive operation through final output.
        internal bool TryClear(out WorldSession? cleared, out ActionPublication? receipt, out string reason)
        {
            cleared = null; receipt = null; reason = "experienceContinuationClearUnavailable";
            RefuseBoundSpellReentry(binding.Connection);
            if (pending.Phase != LegacyExperienceContinuationPhase.Ready || context.WorldData is not { } catalog) return false;
            WorldSession final = null!; object identity = null!; ActionPublication publication = null!;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, current);
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    if (!store._sessions.TryGetValue(binding.Connection, out var holder) ||
                        !store.BindingIsCurrentPlain(binding, holder, current)) return false;
                    LegacyCombatExperienceMetadataWitness metadata;
                    WorldSession proposed;
                    LegacyNativeAttributeMapProof oldAttributes, oldPaid, newAttributes, newPaid;
                    try
                    {
                        metadata = new(catalog);
                        oldAttributes = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
                        oldPaid = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.PaidNobility);
                        if (pending.CurrentLevel < 140 &&
                            (!catalog.LevelExperienceByTypeAndLevel.TryGetValue((0, pending.CurrentLevel), out var threshold) ||
                             threshold.Experience <= 0 || pending.RemainingExperience >= checked((ulong)threshold.Experience))) return false;
                        var character = LegacyExperienceContinuationCodec.WithState(current.Character, current.AccountId, null);
                        // Clearing only the marker still serializes the full
                        // canonical persisted scalar projection below.
                        character = new WorldPacketHandlers.BoundHitpointsInputs(current.Character, character).After;
                        proposed = current with { Character = character };
                        newAttributes = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.Attributes);
                        newPaid = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.PaidNobility);
                        if (!LegacyMailboxPackageMutationFence.CanMutateSession(store.MailboxPackages, current, proposed)) return false;
                        publication = new ActionPublication(store, context, frame, holder.NativeClientIdentity, binding, proposed,
                            LegacyActionState.RequireCurrent(proposed.Character));
                    }
                    catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                    { return false; }

                    string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                    string failure = "experienceContinuationClearImageInvalid";
                    bool saved = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                    {
                        byte[] before, after;
                        try
                        {
                            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationClear", out before, out _) ||
                                !IsHeldCurrentPlain(before) || !metadata.IsCurrentPlain()) return false;
                            var scalarImage = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(before, proposed.Character);
                            after = LegacyExperienceContinuationCodec.ProjectCharacterImage(before, scalarImage,
                                current.AccountId, checked((uint)current.Character.Uid), pending, null);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                            InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                        { return false; }
                        if (!IsHeldCurrentPlain(before) || !metadata.IsCurrentPlain() || !oldAttributes.IsCurrentPlain() ||
                            !oldPaid.IsCurrentPlain() || !newAttributes.IsCurrentPlain() || !newPaid.IsCurrentPlain()) return false;
                        bool written; bool uncertain; string detail;
                        try
                        {
                            var permit = IssueContinuationFilePermit(store, context, binding, current, path, before, after, clear: this);
                            written = CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, after, permit, out uncertain, out detail);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                        { written = false; uncertain = true; detail = error.GetType().Name; }
                        if (!written)
                        {
                            if (!uncertain) { failure = "experienceContinuationClearReplace:" + detail; return false; }
                            FenceContinuationOutcome(holder, binding, new InvalidOperationException("experienceContinuationClearOutcomeUnproven:" + detail));
                            throw new InvalidOperationException("experienceContinuationClearOutcomeUnproven");
                        }
                        try
                        {
                            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationCleared", out var observed, out _) ||
                                !observed.AsSpan().SequenceEqual(after) || !IsHeldCurrentPlain(before) || !metadata.IsCurrentPlain() ||
                                !oldAttributes.IsCurrentPlain() || !oldPaid.IsCurrentPlain() || !newAttributes.IsCurrentPlain() || !newPaid.IsCurrentPlain())
                                throw new InvalidOperationException("experienceContinuationClearOwnerChanged");
                            identity = holder.NativeClientIdentity; final = proposed;
                            holder.PublishPrepared(final, identity); publication.ConfirmCommitted();
                            store.RecordCommittedContinuationRetirement(context, binding, current, final, after);
                        }
                        catch (Exception error) { FenceContinuationOutcome(holder, binding, error); throw; }
                        return true;
                    });
                    if (!saved) { reason = failure; return false; }
                }
            }
            try { store.NotifyPublishedSessionChange(binding.Connection, current, final, identity, binding); }
            catch (Exception error) { publication.FenceCommittedLevelUpFailure(error); throw; }
            cleared = final; receipt = publication; reason = string.Empty;
            return true;
        }
    }
}
