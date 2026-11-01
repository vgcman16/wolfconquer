namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryEnterDragonhowlExperienceWriter(OwnedDragonhowlExperienceAward token,
        WorldPacketHandlers.OwnedCombatExperience purpose, WorldPacketHandlerContext context,
        WorldSession expected, out IDisposable? scope)
    {
        scope = null;
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state) || state.Published is not null ||
            !state.Started || !ReferenceEquals(state.Context, context) || !ReferenceEquals(state.Before, expected) ||
            !ReferenceEquals(state.Purpose, purpose) || state.Team.Death.Source.Owner.Plan is not { } plan) return false;
        var owner = state.Team.Death.Source.Owner;
        if (!Monitor.IsEntered(owner.Frame.RequireActionLifecycleGate(this)) || IsCurrentThreadInsideAnyConnectionGate) return false;
        var held = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
        try
        {
            if (!DragonhowlExperienceTurnCurrent(plan, token, state) ||
                !ReferenceEquals(owner.Current[context.Connection], expected) ||
                !DragonhowlExperienceBeforeMatches(owner, state.Data.Member.Binding, expected,
                    state.Data.Member.CalculatedBefore)) return false;
            scope = held; return true;
        }
        finally { if (scope is null) held.Dispose(); }
    }

    internal void ConfirmDragonhowlExperiencePublication(OwnedDragonhowlExperienceAward token,
        WorldPacketHandlers.OwnedCombatExperience purpose, WorldSession before, WorldSession after,
        ActionPublication receipt, bool continuationCredit)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state) || state.Published is not null ||
            !ReferenceEquals(state.Purpose, purpose) || !ReferenceEquals(state.Before, before) ||
            !ReferenceEquals(receipt.Published, after) || !state.Started || state.Complete)
            throw new InvalidOperationException("ownedDragonhowlExperiencePublicationNotPrepared");
        var owner = state.Team.Death.Source.Owner;
        var binding = state.Data.Member.Binding;
        if (!Monitor.IsEntered(owner.Frame.RequireActionLifecycleGate(this)) ||
            !ReferenceEquals(owner.Current[binding.Connection], before) ||
            !IsLevelUpRebuildOwnerHeld(binding, after, state.Context!) ||
            !receipt.TryCurrent(out var actual) || !ReferenceEquals(actual, after))
            throw new InvalidOperationException("ownedDragonhowlExperiencePublicationOwnerChanged");
        if (continuationCredit)
        {
            var pending = LegacyExperienceContinuationCodec.ReadCharacter(after.Character, after.AccountId);
            if (pending is null || pending.Phase != LegacyExperienceContinuationPhase.Ready || pending.StepIndex != 0 ||
                pending.CreditedExperience != state.Data.Gain || after.Character.Level != before.Character.Level)
                throw new InvalidOperationException("ownedDragonhowlExperienceCreditChanged");
        }
        else if (!DragonhowlExperienceSessionMatches(after, before with { Character = state.Data.ProjectedAfter.Character }))
            throw new InvalidOperationException("ownedDragonhowlExperienceScalarOutcomeChanged");
        // Exact real writer successor is retained before publication observers.
        state.Published = after; state.ScalarReceipt = receipt; state.CreditPublished = continuationCredit;
        owner.Current[binding.Connection] = after;
        if (!DragonhowlHeldCurrent(owner)) throw new InvalidOperationException("ownedDragonhowlExperienceParticipantsChanged");
    }

    internal bool BindDragonhowlExperienceContinuation(OwnedDragonhowlExperienceAward token,
        ExperienceContinuationExecution execution)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state) || !state.CreditPublished ||
            state.Continuation is not null || !ReferenceEquals(execution.Current, state.Published) ||
            !ReferenceEquals(execution.Binding, state.Data.Member.Binding) ||
            !execution.TryCurrent(out var actual) || !ReferenceEquals(actual, state.Published)) return false;
        state.Continuation = execution; return true;
    }

    internal void ConfirmDragonhowlExperienceContinuation(OwnedDragonhowlExperienceAward token,
        ExperienceContinuationExecution original, WorldPacketHandlers.CompletedExperienceContinuation terminal)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state) || !state.CreditPublished ||
            state.Terminal is not null || state.Complete || !ReferenceEquals(state.Continuation, original))
            throw new InvalidOperationException("ownedDragonhowlExperienceTerminalNotPrepared");
        var owner = state.Team.Death.Source.Owner;
        var binding = state.Data.Member.Binding;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (!terminal.TryResolveHeld(original, out var after) ||
                state.Data.Levels is not { } levels || !terminal.MatchesCompletedReceiveData(binding, after, levels) ||
                !DragonhowlExperienceOtherOwnersCurrent(owner, binding) ||
                !DragonhowlExperienceFinalScalarsMatch(after, state.Data.ProjectedAfter))
                throw new InvalidOperationException("ownedDragonhowlExperienceTerminalChanged");
            state.Published = after; state.Terminal = terminal;
            owner.Current[binding.Connection] = after;
            if (!DragonhowlHeldCurrent(owner) || !state.Team.Membership.IsCurrentPlain())
                throw new InvalidOperationException("ownedDragonhowlExperienceTerminalParticipantsChanged");
        }
    }

    private bool DragonhowlExperienceOtherOwnersCurrent(DragonhowlOwnerState owner, LegacyOrdinarySpellBinding recipient)
    {
        if (!Monitor.IsEntered(owner.Frame.RequireActionLifecycleGate(this)) || owner.Failed || owner.Completed ||
            !owner.Frame.IsActionLifecycleCurrent(this, owner.Context) ||
            owner.Context.TradeOperations?.OwnsActiveExclusiveLease(owner.Context.FrameOperationLease) != true) return false;
        foreach (var pair in owner.Current)
        {
            if (ReferenceEquals(pair.Key, recipient.Connection)) continue;
            var binding = owner.Bindings[pair.Key];
            if (!_sessions.TryGetValue(pair.Key, out var holder) || !ReferenceEquals(holder.Read(), pair.Value) ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !BindingIsCurrentPlain(binding, holder, pair.Value)) return false;
        }
        return true;
    }

    private static bool DragonhowlExperienceFinalScalarsMatch(WorldSession actual, WorldSession calculated) =>
        actual.AccountId == calculated.AccountId && actual.Character.Uid == calculated.Character.Uid &&
        actual.Character.Level == calculated.Character.Level && actual.Character.Experience == calculated.Character.Experience &&
        actual.Character.Class == calculated.Character.Class && actual.Character.Reborn == calculated.Character.Reborn &&
        actual.Character.Strength == calculated.Character.Strength && actual.Character.Agility == calculated.Character.Agility &&
        actual.Character.Vitality == calculated.Character.Vitality && actual.Character.Spirit == calculated.Character.Spirit &&
        actual.Character.AttributePoints == calculated.Character.AttributePoints &&
        actual.Character.MinHitPoints == calculated.Character.MinHitPoints &&
        ReferenceEquals(actual.PlayerItems, calculated.PlayerItems) &&
        LegacyExperienceContinuationCodec.ReadCharacter(actual.Character, actual.AccountId) is null;

    internal bool CompleteDragonhowlExperienceAward(OwnedDragonhowlExperienceAward token,
        WorldPacketHandlers.CompletedExperienceContinuation? terminal)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state)) return false;
        var owner = state.Team.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (state.Complete || state.Published is not { } published || !ReferenceEquals(owner.Current[state.Data.Member.Binding.Connection], published) ||
                !DragonhowlHeldCurrent(owner) || !state.Team.Membership.IsCurrentPlain() ||
                state.Team.Next >= state.Team.Awards.Count || !ReferenceEquals(state.Team.Awards[state.Team.Next], token)) return false;
            if (state.CreditPublished ? !ReferenceEquals(terminal, state.Terminal) || terminal is null || state.Continuation is null ||
                    !terminal.TryResolveHeld(state.Continuation, out var current) || !ReferenceEquals(current, published) :
                terminal is not null || state.ScalarReceipt is null || !state.ScalarReceipt.TryCurrent(out var scalar) || !ReferenceEquals(scalar, published))
                return false;
            state.Complete = true;
            owner.CompletedAwards[state.Data.Member.Binding.Connection] = state;
            if (state.CreditPublished) owner.CompletedExperience[state.Data.Member.Binding.Connection] = state;
            state.Team.Next++;
            state.Team.Complete = state.Team.Next == state.Team.Awards.Count;
            return true;
        }
    }

    internal bool IsDragonhowlTeamExperienceComplete(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlTeamExperience token)
    {
        if (!dragonhowlTeamExperience.TryGetValue(token, out var state)) return false;
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            return state.Started && state.Complete && state.Next == state.Awards.Count &&
                ReferenceEquals(owner.Plan, plan) && DragonhowlHeldCurrent(owner) && state.Membership.IsCurrentPlain() &&
                state.Awards.All(token => dragonhowlExperienceAwards.TryGetValue(token, out var award) && award.Complete);
        }
    }
}
