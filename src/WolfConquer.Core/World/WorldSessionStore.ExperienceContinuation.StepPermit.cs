using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private static readonly ConditionalWeakTable<ExperienceStepCommitPermit, ExperienceContinuationExecution.StepPlan> CommittedExperienceSteps = new();

    // Only the real step writer may issue this after exact after-image proof
    // while it still holds its actor and file locks. No public registration.
    private static ExperienceStepCommitPermit IssueCommittedExperienceStep(ExperienceContinuationExecution.StepPlan step)
    {
        var permit = new ExperienceStepCommitPermit();
        CommittedExperienceSteps.Add(permit, step);
        return permit;
    }

    internal sealed class ExperienceStepCommitPermit
    {
        internal bool Consume(ExperienceContinuationExecution.StepPlan step) =>
            CommittedExperienceSteps.TryGetValue(this, out var actual) && ReferenceEquals(actual, step) &&
            CommittedExperienceSteps.Remove(this);
    }
}
