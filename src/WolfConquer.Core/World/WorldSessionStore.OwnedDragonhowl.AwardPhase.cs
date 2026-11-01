using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlAwardPhase
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlAwardPhase(WorldSessionStore store) => this.store = store;
        internal bool InputsAreCurrent() => store.dragonhowlAwardPhases.TryGetValue(this, out var state) &&
            !state.Owner.Failed && state.Award.Data.Levels?.RecordedInputsAreCurrent() == true;
        internal bool IsPreparedPhase(WorldPacketHandlers.DragonhowlPhase phase) =>
            store.dragonhowlAwardPhases.TryGetValue(this, out var state) && state.Owner.Plan is null &&
            !state.Owner.Started && store.DragonhowlHeldCurrent(state.Owner) && !state.Consumed &&
            ReferenceEquals(phase.FutureAward, this) && ReferenceEquals(phase.Connection, state.Binding.Connection) &&
            ReferenceEquals(phase.Before, state.Before) && ReferenceEquals(phase.After, state.After) && InputsAreCurrent();
    }

    private sealed class DragonhowlAwardPhaseState(DragonhowlOwnerState owner, LegacyOrdinarySpellBinding binding,
        DragonhowlExperienceAwardState award, WorldSession before, WorldSession after)
    {
        internal readonly DragonhowlOwnerState Owner = owner;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly DragonhowlExperienceAwardState Award = award;
        internal readonly WorldSession Before = before, After = after;
        internal bool Consumed;
    }
    private readonly ConditionalWeakTable<OwnedDragonhowlAwardPhase, DragonhowlAwardPhaseState> dragonhowlAwardPhases = new();

    internal OwnedDragonhowlAwardPhase PrepareDragonhowlAwardPhase(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, WorldSession before, WorldSession after)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null ||
            !IsOwnedDragonhowlBeforeCurrent(purpose) || !owner.Bindings.TryGetValue(binding.Connection, out var actual) ||
            !ReferenceEquals(binding, actual) || !owner.PlannedAwards.TryGetValue(binding.Connection, out var award) ||
            award.Data.Levels is not { } levels) throw new InvalidOperationException("ownedDragonhowlAwardPhaseNotPrepared");
        levels.ValidateReceivePhase(before, after);
        var token = new OwnedDragonhowlAwardPhase(this);
        dragonhowlAwardPhases.Add(token, new(owner, binding, award, before, after));
        return token;
    }

    private bool ConsumeDragonhowlAwardPhase(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
        OwnedDragonhowlAwardPhase token, WorldSession current)
    {
        if (!dragonhowlAwardPhases.TryGetValue(token, out var state) || state.Consumed ||
            !dragonhowlOwners.TryGetValue(plan.Owner, out var owner) || !ReferenceEquals(owner, state.Owner) ||
            !ReferenceEquals(owner.Plan, plan) || owner.NextPhase != index || !owner.Started || !owner.Committed ||
            !DragonhowlHeldCurrent(owner) || !ReferenceEquals(plan.Phases[index].FutureAward, token) ||
            !state.Award.Complete || !owner.CompletedAwards.TryGetValue(state.Binding.Connection, out var award) ||
            !ReferenceEquals(award, state.Award) || !ReferenceEquals(owner.Current[state.Binding.Connection], current) ||
            !WorldPacketHandlers.DragonhowlLevelExperienceData.FutureSessionMatches(current, state.Before) ||
            !token.InputsAreCurrent()) return false;
        if (state.Award.Data.Levels?.FinalReceiveData is { } future &&
            !IsDragonhowlQuerySuccessor(plan.Owner, state.Binding, current, future)) return false;
        if (!state.Award.CreditPublished && state.Award.ScalarReceipt is null ||
            !state.Award.CreditPublished && !ReferenceEquals(state.Award.ScalarReceipt!.Published, state.Award.Published)) return false;
        state.Consumed = true;
        return true;
    }
}
