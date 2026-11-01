using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool DragonhowlMonsterRewardInputsCurrent(OwnedDragonhowlMonsterDeath token) =>
        dragonhowlMonsterDeaths.TryGetValue(token, out var death) && !death.Source.Owner.Failed &&
        dragonhowlDropsByDeath.TryGetValue(death, out var drops) && !drops.Disposed && drops.Recipe.InputsAreCurrentPlain() &&
        death.Steps.All(step => dragonhowlDeathCps.TryGetValue(step, out var row) && row.Data.InputsAreCurrentPlain());

    internal bool TryReadDragonhowlMonsterDeathPreparation(OwnedDragonhowlMonsterDeath death,
        out WorldPacketHandlerContext context, out WorldSession calculatedActor,
        out LegacyMonsterInstance monster, out Monster family)
    {
        context = null!; calculatedActor = null!; monster = null!; family = null!;
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var state) || state.Active ||
            state.Source.Owner.Plan is not null || state.Source.Owner.Started || state.Source.Owner.Failed ||
            !DragonhowlHeldCurrent(state.Source.Owner) || !state.Source.BeforeTargetCurrent() ||
            !state.Source.Owner.Frame.TryReadOwnedDragonhowl(state.Source.Owner.Request, out _) ||
            state.Source.CalculatedActor is not { } actor || state.Source.Monster is not { } original ||
            state.Source.Family is not { } row) return false;
        context = state.Source.Owner.Context; calculatedActor = actor; monster = original; family = row;
        return true;
    }

    internal bool IsDragonhowlMonsterRewardsComplete(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death)
    {
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var state)) return false;
        var owner = state.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            return DragonhowlMonsterRewardsCompleteHeld(plan, state);
        }
    }

    private bool DragonhowlMonsterRewardsCompleteHeld(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        DragonhowlMonsterDeathState death)
    {
        if (!death.Active || death.Steps.Count != 2 || death.NextStep != death.Steps.Count ||
            !DragonhowlDeathHeldCurrent(plan, death) || !HasCompletedDragonhowlDropHistory(death)) return false;
        dragonhowlDropsByDeath.TryGetValue(death, out var drops);
        WorldSession? prior = drops!.Actor;
        foreach (var token in death.Steps)
        {
            if (!dragonhowlDeathCps.TryGetValue(token, out var step) || !ReferenceEquals(step.Death, death) ||
                !step.Started || !step.OutcomeConfirmed || !step.OutputStarted || !step.OutputComplete ||
                step.Before is not { } before || step.After is not { } after ||
                prior is not null && !ReferenceEquals(before, prior) ||
                !step.Data.MatchesBefore(before) || !step.Data.MatchesAfter(after) ||
                (step.Data.Amount == 0 ? step.Publication is not null || !ReferenceEquals(before, after) :
                    step.Publication is not { } receipt || !ReferenceEquals(receipt.Published, after))) return false;
            prior = after;
        }
        // Historical CP1/drop receipts are not reused as live output owners
        // after CP2. Their actual successor chain ends in the current actor.
        return ReferenceEquals(prior, death.Source.Owner.Current[death.Source.Owner.Context.Connection]);
    }

    private bool HasCompletedDragonhowlDropHistory(DragonhowlMonsterDeathState death) =>
        dragonhowlDropsByDeath.TryGetValue(death, out var drops) && drops.Prepared && drops.Started &&
        drops.Next == drops.Attempts.Count && drops.Receipt is { } receipt &&
        dragonhowlDropReceipts.TryGetValue(receipt, out var registered) && ReferenceEquals(registered, drops) &&
        drops.Actor is not null && drops.Qualities.All(row => row.Committed) &&
        drops.Attempts.All(token => dragonhowlGroundAttempts.TryGetValue(token, out var step) &&
            ReferenceEquals(step.Drops, drops) && step.Committed && step.OutputCompleted);
}
