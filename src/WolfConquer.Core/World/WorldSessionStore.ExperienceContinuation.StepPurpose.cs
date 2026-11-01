using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class ContinuationStepSource(ExperienceContinuationExecution execution,
        ExperienceContinuationExecution.StepPlan? step, byte[] before)
    {
        internal readonly ExperienceContinuationExecution Execution = execution;
        internal readonly ExperienceContinuationExecution.StepPlan? Step = step;
        internal readonly byte[] Before = before;
        internal WorldSession? Committed;
        internal ActionPublication? Publication;
        internal int QueryStarted;
    }
    private static readonly ConditionalWeakTable<OwnedContinuationStep, ContinuationStepSource> ContinuationStepSources = new();

    // Called only by the real step writer, after it has prepared the exact
    // transition under the held native owner. No external callback registers it.
    private static OwnedContinuationStep IssueContinuationStepPurpose(ExperienceContinuationExecution execution,
        ExperienceContinuationExecution.StepPlan step)
    {
        var before = step.CopyBeforeImage();
        if (!step.Covers(execution.Binding, execution.Current, step.Proposed, before))
            throw new InvalidOperationException("experienceContinuationStepPurposeInvalid");
        var purpose = new OwnedContinuationStep();
        ContinuationStepSources.Add(purpose, new(execution, step, before));
        return purpose;
    }

    private static void BindCommittedContinuationStep(OwnedContinuationStep purpose,
        WorldSession committed, ActionPublication publication)
    {
        if (!ContinuationStepSources.TryGetValue(purpose, out var source) || source.Step is null ||
            source.Committed is not null || !ReferenceEquals(source.Step.Proposed, committed) ||
            !ReferenceEquals(publication.Published, committed))
            throw new InvalidOperationException("experienceContinuationStepReceiptInvalid");
        source.Committed = committed; source.Publication = publication;
    }

    internal sealed partial class ExperienceContinuationExecution
    {
        internal bool TryIssueResumeQueryPurpose(out OwnedContinuationStep? purpose)
        {
            purpose = null;
            if (pending.Phase is not (LegacyExperienceContinuationPhase.QueryPending or LegacyExperienceContinuationPhase.RestorePending) ||
                !TryCurrent(out var actual) || !ReferenceEquals(actual, current) ||
                Interlocked.CompareExchange(ref resumePurposeIssued, 1, 0) != 0) return false;
            var issued = new OwnedContinuationStep();
            var source = new ContinuationStepSource(this, null, image.ToArray()) { Committed = current };
            ContinuationStepSources.Add(issued, source);
            purpose = issued; return true;
        }
    }

    internal sealed class OwnedContinuationStep
    {
        internal bool CoversRebuild(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed) =>
            ContinuationStepSources.TryGetValue(this, out var source) &&
            ReferenceEquals(context, source.Execution.OperationContext) &&
            ReferenceEquals(context.Connection, binding.Connection) && ReferenceEquals(context.Sessions, binding.Store) &&
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
            source.Step is { } step && step.Covers(binding, expected, proposed, source.Before);

        // Cold pending-query/restore is a separate saved-admission-origin
        // path. It never claims a prior process's committed step receipt.
        internal bool CoversResume(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession expected) => ContinuationStepSources.TryGetValue(this, out var source) && source.Step is null &&
            ReferenceEquals(context, source.Execution.OperationContext) && ReferenceEquals(binding, source.Execution.Binding) &&
            ReferenceEquals(expected, source.Committed) && source.Execution.IsHeldCurrentPlain(source.Before);

        internal bool TryConsumeQueryStart(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession committed, out WorldSession current)
        {
            current = null!;
            if (!ContinuationStepSources.TryGetValue(this, out var source) ||
                !ReferenceEquals(context, source.Execution.OperationContext) || !ReferenceEquals(binding, source.Execution.Binding) ||
                !ReferenceEquals(source.Committed, committed) || context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true)
                return false;
            bool valid = source.Step is null
                ? source.Execution.TryCurrent(out current) && ReferenceEquals(current, committed)
                : source.Publication is { } publication && publication.TryCurrent(out current) && ReferenceEquals(current, committed);
            return valid && Interlocked.CompareExchange(ref source.QueryStarted, 1, 0) == 0;
        }
    }
}
