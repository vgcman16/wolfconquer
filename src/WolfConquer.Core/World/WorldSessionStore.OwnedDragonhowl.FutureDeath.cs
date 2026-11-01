using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlFutureDeath
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlFutureDeath(WorldSessionStore store) => this.store = store;
        internal WorldSession Projected => store.dragonhowlFutureDeaths.TryGetValue(this, out var state)
            ? state.Data.Session : throw new InvalidOperationException("ownedDragonhowlFutureDeathNotIssued");
        internal bool InputsAreCurrentPlain() => store.dragonhowlFutureDeaths.TryGetValue(this, out var state) &&
            !state.Owner.Failed && state.Metadata.IsCurrentPlain() &&
            (state.Actual is not null || state.Flashing is null || state.Flashing.IsNativeCurrentPlain()) &&
            (state.Actual is not null || store.DragonhowlFutureDeathQueryInputsCurrent(state));
        internal bool IsPreparedPhase(WorldPacketHandlers.DragonhowlPhase phase) =>
            store.dragonhowlFutureDeaths.TryGetValue(this, out var state) && state.Owner.Plan is null &&
            !state.Owner.Started && store.DragonhowlHeldCurrent(state.Owner) &&
            ReferenceEquals(phase.FutureDeath, this) && ReferenceEquals(phase.Connection, state.Binding.Connection) &&
            ReferenceEquals(phase.Before, state.Before) &&
            WorldPacketHandlers.DragonhowlLevelExperienceData.FutureSessionMatches(phase.After, state.Data.Session) &&
            InputsAreCurrentPlain();
    }

    private sealed record DragonhowlFutureDeathState(DragonhowlOwnerState Owner, LegacyOrdinarySpellBinding Binding,
        DragonhowlExperienceAwardState Award, WorldSession Before, WorldSession Calculated,
        uint Damage, WorldPacketHandlers.FuturePlayerDeathData Data,
        WorldPacketHandlers.FuturePlayerDeathData.Images Images, LegacyOwnedPhysicalMetadataWitness Metadata,
        LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease? Flashing,
        DragonhowlScalarQueryOrigin? ScalarQuery)
    {
        internal OwnedDragonhowlDeath? Actual;
    }
    private readonly ConditionalWeakTable<OwnedDragonhowlFutureDeath, DragonhowlFutureDeathState> dragonhowlFutureDeaths = new();

    private bool DragonhowlFutureDeathQueryInputsCurrent(DragonhowlFutureDeathState state)
    {
        if (state.ScalarQuery is not { } scalar) return true;
        if (!state.Owner.Current.TryGetValue(state.Binding.Connection, out var current)) return false;
        return state.Award.Complete && state.Owner.Plan is { } plan
            ? scalar.Query.IsCurrentForDragonhowl(state.Binding, current, plan.Owner)
            : scalar.Query.IsCurrentPlain(state.Binding, current);
    }

    internal OwnedDragonhowlFutureDeath PrepareDragonhowlFutureDeath(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, WorldSession calculatedActor, WorldSession before,
        WorldSession calculated, uint damage)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null ||
            !IsOwnedDragonhowlBeforeCurrent(purpose) || !owner.Bindings.TryGetValue(binding.Connection, out var bound) ||
            !ReferenceEquals(bound, binding) || !owner.PlannedAwards.TryGetValue(binding.Connection, out var award) ||
            award.Data.Levels is not { } levels ||
            before.Character.MinHitPoints <= 0 || damage < before.Character.MinHitPoints ||
            calculated.AccountId != binding.AccountId || calculated.Character.Uid != binding.Uid ||
            !ReferenceEquals(calculated.NativeGameClient, binding.Client) ||
            !WorldPacketHandlers.DragonhowlLevelExperienceData.FutureSessionMatches(calculated,
                WorldPacketHandlers.FuturePlayerDeathData.ProjectReceivePrefix(before, calculatedActor.Character.Uid)) ||
            owner.Context.WorldData is not { } catalog || owner.Context.TimeProvider is not { } clock)
            throw new InvalidOperationException("ownedDragonhowlFutureDeathRequiresPreparedAward");
        var metadata = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(binding, catalog, before);
        var future = levels.FinalReceiveData;
        var scalarQuery = future is null ? CaptureDragonhowlScalarQuery(owner, binding) : null;
        if (!binding.Player.BitVector.TryCaptureWindWalker(out var wind) || wind is null ||
            wind.Present && wind.RemoveOnDeath) throw new InvalidOperationException("ownedDragonhowlFutureDeathNativeStatusUnavailable");
        var flashing = LegacyNativeStatusFlagsBigVector32.CaptureOwnedPhysicalDeathFlashing(binding,
            owner.Current[binding.Connection], wind);
        metadata.Extend(calculated); metadata.RequireNativeQueryInputs(calculated);
        var data = WorldPacketHandlers.FuturePlayerDeathData.Project(calculatedActor, calculated, future, catalog,
            owner.Context.LegacyClock ?? LegacySignedMillisecondClock.System, clock, owner.Context.ProtocolProfile, scalarQuery?.Values);
        metadata.Extend(data.Session); metadata.RequireNativeQueryInputs(data.Session);
        var roots = owner.Frame.RequireBasicPhysicalFileRoots(this, binding);
        var images = levels.ProjectFutureDeathImages(data, roots.Users, roots.Items, roots.Spells);
        if (!metadata.IsCurrentPlain() || !DragonhowlHeldCurrent(owner) || !levels.IsCurrentPlain())
            throw new InvalidOperationException("ownedDragonhowlFutureDeathInputsChanged");
        var token = new OwnedDragonhowlFutureDeath(this);
        dragonhowlFutureDeaths.Add(token, new(owner, binding, award, before, calculated, damage, data, images, metadata, flashing, scalarQuery));
        return token;
    }

    // This is the first real death preparation, after the actual XP/query
    // receipt. It never treats the calculated future session as a live holder.
    private bool TryBindDragonhowlFutureDeath(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
        OwnedDragonhowlFutureDeath token, out OwnedDragonhowlDeath? purpose)
    {
        purpose = null;
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var owner) || !ReferenceEquals(owner.Plan, plan) ||
            owner.NextPhase != index || !owner.Started || !owner.Committed || !DragonhowlHeldCurrent(owner) ||
            !ReferenceEquals(plan.Phases[index].FutureDeath, token) ||
            !dragonhowlFutureDeaths.TryGetValue(token, out var state) || !ReferenceEquals(state.Owner, owner) ||
            state.Actual is not null || !token.InputsAreCurrentPlain() || !state.Award.Complete ||
            !owner.CompletedAwards.TryGetValue(state.Binding.Connection, out var completed) ||
            !ReferenceEquals(completed, state.Award) ||
            !owner.Current.TryGetValue(state.Binding.Connection, out var current) ||
            !WorldPacketHandlers.DragonhowlLevelExperienceData.FutureSessionMatches(current, state.Before) ||
            current.Character.MinHitPoints <= 0 || state.Damage < current.Character.MinHitPoints) return false;
        if (state.Award.Data.Levels?.FinalReceiveData is { } future)
        {
            if (!IsDragonhowlQuerySuccessor(plan.Owner, state.Binding, current, future)) return false;
        }
        else if (state.Award.CreditPublished || state.ScalarQuery is not { } scalar ||
            !scalar.Query.IsCurrentForDragonhowl(state.Binding, current, plan.Owner)) return false;
        // Replay only this closed receive-prefix edit onto the actual query
        // successor. In particular, a preflight clock sample is not copied
        // over the real query's completed timestamp or other runtime fields.
        var candidate = WorldPacketHandlers.FuturePlayerDeathData.ProjectReceivePrefix(current,
            owner.Current[owner.Context.Connection].Character.Uid);
        var actual = new OwnedDragonhowlDeath(this);
        var issued = new DragonhowlDeathState(owner, state.Binding, owner.Current[owner.Context.Connection], current, candidate);
        dragonhowlDeaths.Add(actual, issued);
        issued.Death = WorldPacketHandlers.PrepareOwnedDragonhowlDeath(actual);
        if (!actual.IsCurrentPlain() || !state.Data.MatchesActual(issued.Death))
            throw new InvalidOperationException("ownedDragonhowlFutureDeathActualOutcomeChanged");
        issued.Bound = true;
        state.Actual = actual;
        purpose = actual; return true;
    }
}
