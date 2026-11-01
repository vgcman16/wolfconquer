using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ExperienceContinuationExecution
    {
        // Unwired until continuation query phases, generic marker fences and
        // pre-AnswerOk resume are joined. One exact durable level/stat step;
        // it does not send output or mark the query/restore as complete.
        internal bool TryCommitNextStep(out WorldSession? committed, out LegacyExperienceContinuation? next,
            out ActionPublication? receipt, out WorldPacketHandlers.PreparedLevelUpRebuild? rebuild,
            out LegacyClassLevelStatProgressionChange progression, out string reason)
        {
            committed = null; next = null; receipt = null; rebuild = null; progression = default; reason = "experienceContinuationStepUnavailable";
            RefuseBoundSpellReentry(binding.Connection);
            WorldSession final = null!; object identity = null!; ActionPublication publication = null!;
            WorldPacketHandlers.PreparedLevelUpRebuild preparedQuery = null!; StepPlan preparedStep = null!;
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
                    string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                    StepPlan? candidate = null;
                    bool planned = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                        CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationStepPlan", out var captured, out _) &&
                        StepPlan.TryPrepare(this, captured, out candidate));
                    if (!planned || candidate is null) return false;
                    preparedStep = candidate;
                    OwnedContinuationStep purpose;
                    LegacyNativePlayerRuntime.ExperienceStepNativePublication nativeStep;
                    try
                    {
                        purpose = IssueContinuationStepPurpose(this, preparedStep);
                        if (!WorldPacketHandlers.PreparedLevelUpRebuild.TryPrepare(context, purpose, binding, current,
                                preparedStep.Proposed, out var query) || query is null) return false;
                        preparedQuery = query;
                        nativeStep = LegacyNativePlayerRuntime.ExperienceStepNativePublication.Prepare(binding, current,
                            preparedStep, preparedStep.CopyBeforeImage());
                        publication = new ActionPublication(store, context, frame, holder.NativeClientIdentity, binding,
                            preparedStep.Proposed, LegacyActionState.RequireCurrent(preparedStep.Proposed.Character));
                    }
                    catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                    { return false; }

                    string[] paths = preparedQuery.LockedSpellPath is { } spellPath ? [path, spellPath] : [path];
                    string failure = "experienceContinuationStepImageInvalid";
                    bool saved = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    {
                        byte[] before, after;
                        try
                        {
                            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationStep", out before, out _) ||
                                !preparedStep.IsHeldCurrentPlain(before) || !preparedQuery.IsCurrentPlain() || !nativeStep.IsCurrentPlain()) return false;
                            preparedQuery.BindCommitted(preparedStep.Proposed);
                            after = preparedStep.ProjectCharacterImage();
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                            InvalidOperationException or OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                        { return false; }
                        if (!preparedStep.IsHeldCurrentPlain(before) || !preparedQuery.IsCurrentPlain() || !nativeStep.IsCurrentPlain()) return false;
                        bool written; bool uncertain; string detail;
                        try
                        {
                            var permit = IssueContinuationFilePermit(store, context, binding, current, path, before, after, step: preparedStep);
                            written = CharacterSnapshotFileCoordinator.TryReplaceContinuationCapturedImage(path, before, after, permit, out uncertain, out detail);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                        { written = false; uncertain = true; detail = error.GetType().Name; }
                        if (!written)
                        {
                            if (!uncertain) { failure = "experienceContinuationStepReplace:" + detail; return false; }
                            FenceContinuationOutcome(holder, binding, new InvalidOperationException("experienceContinuationStepOutcomeUnproven:" + detail));
                            throw new InvalidOperationException("experienceContinuationStepOutcomeUnproven");
                        }
                        try
                        {
                            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationStepCommitted", out var observed, out _) ||
                                !observed.AsSpan().SequenceEqual(after) || !preparedStep.IsHeldCurrentPlain(before) ||
                                !preparedQuery.IsCurrentPlain() || !nativeStep.IsCurrentPlain())
                                throw new InvalidOperationException("experienceContinuationStepOwnerChanged");
                            var permit = IssueCommittedExperienceStep(preparedStep);
                            nativeStep.PublishCommitted(permit);
                            identity = holder.NativeClientIdentity; final = preparedStep.Proposed;
                            holder.PublishPrepared(final, identity); publication.ConfirmCommitted();
                            BindCommittedContinuationStep(purpose, final, publication);
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
            committed = final; next = preparedStep.Next; receipt = publication; rebuild = preparedQuery;
            progression = preparedStep.Progression; reason = string.Empty;
            return true;
        }
    }
}
