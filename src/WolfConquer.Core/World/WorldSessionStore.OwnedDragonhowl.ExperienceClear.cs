using System.Globalization;
using WolfConquer.Core.Data;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record DragonhowlReadyExperience(DragonhowlExperienceAwardState Award,
        WorldPacketHandlers.CompletedContinuationQuery Query, WorldSession Ready)
    {
        internal ActionPublication? ClearReceipt;
        internal WorldSession? Cleared;
    }
    private readonly ConditionalWeakTable<ExperienceContinuationExecution, DragonhowlReadyExperience> dragonhowlReadyExperience = new();

    internal bool BindDragonhowlExperienceReady(OwnedDragonhowlExperienceAward token,
        ExperienceContinuationExecution original, ExperienceContinuationExecution ready,
        WorldPacketHandlers.CompletedContinuationQuery query)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state) || !ReferenceEquals(state.Continuation, original) ||
            state.Complete || state.Terminal is not null || !ReferenceEquals(ready.Binding, state.Data.Member.Binding) ||
            ready.Pending.Phase != LegacyExperienceContinuationPhase.Ready ||
            !query.TryCurrent(out var actual) || !ReferenceEquals(actual, ready.Current) ||
            !query.CoversPending(ready.Pending) || !ready.TryCurrent(out var current) || !ReferenceEquals(current, actual)) return false;
        var owner = state.Team.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (!DragonhowlExperienceOtherOwnersCurrent(owner, ready.Binding) || !query.TryCurrent(out current) ||
                !ReferenceEquals(current, actual) || !query.CoversPending(ready.Pending)) return false;
            string path = Path.Combine(ready.Binding.UsersRoot, ready.Binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            if (!CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedDragonhowlExperienceReady", out var image, out _) &&
                ready.IsHeldCurrentPlain(image))) return false;
            var lineage = new DragonhowlReadyExperience(state, query, actual);
            dragonhowlReadyExperience.Add(ready, lineage);
            // This successor comes from the actual query/restore checkpoint,
            // not from a fresh Store lookup or an equal future DATA projection.
            owner.Current[ready.Binding.Connection] = actual;
            state.Published = actual;
            return DragonhowlHeldCurrent(owner) && state.Team.Membership.IsCurrentPlain();
        }
    }

    private bool TryEnterDragonhowlExperienceClear(ExperienceContinuationExecution execution, out IDisposable? scope)
    {
        scope = null;
        if (!dragonhowlReadyExperience.TryGetValue(execution, out var ready)) return true;
        var state = ready.Award; var owner = state.Team.Death.Source.Owner;
        if (!Monitor.IsEntered(owner.Frame.RequireActionLifecycleGate(this)) || IsCurrentThreadInsideAnyConnectionGate ||
            ready.Cleared is not null || state.Complete || !ReferenceEquals(execution.Current, ready.Ready)) return false;
        var held = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
        try
        {
            if (!DragonhowlHeldCurrent(owner) || !state.Team.Membership.IsCurrentPlain() ||
                !ready.Query.TryCurrent(out var actual) || !ReferenceEquals(actual, ready.Ready) ||
                !ready.Query.CoversPending(execution.Pending)) return false;
            scope = held; return true;
        }
        finally { if (scope is null) held.Dispose(); }
    }

    private void ConfirmDragonhowlExperienceClear(ExperienceContinuationExecution execution,
        WorldSession final, ActionPublication receipt)
    {
        if (!dragonhowlReadyExperience.TryGetValue(execution, out var ready)) return;
        var state = ready.Award; var owner = state.Team.Death.Source.Owner;
        if (ready.Cleared is not null || state.Complete || !ReferenceEquals(owner.Current[execution.Binding.Connection], ready.Ready) ||
            !ReferenceEquals(receipt.Published, final) || !IsLevelUpRebuildOwnerHeld(execution.Binding, final, execution.OperationContext) ||
            !ready.Query.TryReadTerminalMaximums(ready.Ready, final, out var cache) || cache is null ||
            state.Data.Levels is not { } levels || !levels.MatchesCompletedQuery(execution.Binding, final, cache) ||
            !execution.IsTerminalClearCurrent(final, receipt) || !DragonhowlExperienceOtherOwnersCurrent(owner, execution.Binding))
            throw new InvalidOperationException("ownedDragonhowlExperienceClearSuccessorChanged");
        ready.ClearReceipt = receipt; ready.Cleared = final;
        state.Published = final; owner.Current[execution.Binding.Connection] = final;
        if (!DragonhowlHeldCurrent(owner) || !state.Team.Membership.IsCurrentPlain())
            throw new InvalidOperationException("ownedDragonhowlExperienceClearParticipantsChanged");
    }
}
