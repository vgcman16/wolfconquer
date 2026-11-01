namespace WolfConquer.Core.World;

// Closed sum of two actual issued owners. Neither a DATA death recipe nor a
// caller-supplied successful result can construct a journal22 participant.
internal sealed class LegacyOwnedPhysicalDeathPurpose
{
    private readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation? basic;
    private readonly WorldSessionStore.OwnedDragonhowlDeath? dragonhowl;
    private LegacyOwnedPhysicalDeathPurpose(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation basic)
    { this.basic = basic; if (basic.Death is null || !basic.IsCurrentPlain()) throw Invalid(); }
    private LegacyOwnedPhysicalDeathPurpose(WorldSessionStore.OwnedDragonhowlDeath dragonhowl)
    { this.dragonhowl = dragonhowl; if (!dragonhowl.IsCurrentPlain() || dragonhowl.Death is null) throw Invalid(); }
    internal static LegacyOwnedPhysicalDeathPurpose FromBasic(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual) => new(actual);
    internal static LegacyOwnedPhysicalDeathPurpose FromDragonhowl(WorldSessionStore.OwnedDragonhowlDeath actual) => new(actual);
    internal LegacyOrdinarySpellBinding TargetBinding => basic?.TargetBinding ?? dragonhowl!.TargetBinding;
    internal WorldSession ExpectedTarget => basic?.ExpectedTarget ?? dragonhowl!.ExpectedTarget;
    internal WorldSession Actor => basic?.Actor ?? dragonhowl!.ExpectedActor;
    internal WorldSession Target => basic?.Target ?? dragonhowl!.Death.Proposed;
    internal WorldPacketHandlers.OwnedPhysicalDeathPreparation Death => basic?.Death ?? dragonhowl!.Death;
    // Journal24 may join only the exact actual basic receiver inside this
    // closed purpose; a proc death purpose cannot become a basic hit by shape.
    internal bool IsActualBasic(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual) =>
        basic is not null && dragonhowl is null && ReferenceEquals(basic, actual) && basic.IsCurrentPlain();
    internal bool IsCurrentPlain() => basic is not null ? basic.IsCurrentPlain() : dragonhowl!.IsCurrentPlain();
    internal bool CanonicalOutcomesMatch(WorldSession actor, WorldSession target) => basic is not null
        ? basic.CanonicalOutcomesMatch(actor, target)
        : ReferenceEquals(actor, dragonhowl!.ExpectedActor) && dragonhowl.IsCurrentPlain() &&
            dragonhowl.Death.MatchesDragonhowlCanonical(target);
    private static InvalidOperationException Invalid() => new("physicalDeathRequiresAnActualIssuedPurpose");
}

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlDeath
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlDeath(WorldSessionStore store) => this.store = store;
        private DragonhowlDeathState Data => store.dragonhowlDeaths.TryGetValue(this, out var data) ? data :
            throw new InvalidOperationException("ownedDragonhowlDeathNotIssued");
        internal WorldPacketHandlerContext Context => Data.Owner.Context;
        internal LegacyOrdinarySpellBinding ActorBinding => Data.Owner.Bindings[Data.Owner.Context.Connection];
        internal LegacyOrdinarySpellBinding TargetBinding => Data.Binding;
        internal WorldSession ExpectedActor => Data.ExpectedActor;
        internal WorldSession ExpectedTarget => Data.ExpectedTarget;
        internal WorldSession Calculated => Data.Calculated;
        internal WorldPacketHandlers.OwnedPhysicalDeathPreparation Death => Data.Death ??
            throw new InvalidOperationException("ownedDragonhowlDeathNotPrepared");
        internal bool IsCurrentPlain() => store.dragonhowlDeaths.TryGetValue(this, out var data) &&
            store.DragonhowlHeldCurrent(data.Owner) && !data.Owner.Failed &&
            ReferenceEquals(data.Owner.Current[data.Owner.Context.Connection], data.ExpectedActor) &&
            ReferenceEquals(data.Owner.Current[data.Binding.Connection], data.ExpectedTarget);
    }

    private sealed class DragonhowlDeathState(DragonhowlOwnerState owner, LegacyOrdinarySpellBinding binding,
        WorldSession actor, WorldSession target, WorldSession calculated)
    {
        internal readonly DragonhowlOwnerState Owner = owner;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal WorldSession ExpectedActor = actor, ExpectedTarget = target;
        internal readonly WorldSession Calculated = calculated;
        internal WorldPacketHandlers.OwnedPhysicalDeathPreparation? Death;
        internal bool Bound;
    }
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedDragonhowlDeath, DragonhowlDeathState>
        dragonhowlDeaths = new();

    internal OwnedDragonhowlDeath PrepareDragonhowlDeath(OwnedDragonhowlOwner owner,
        LegacyOrdinarySpellBinding binding, WorldSession calculated, uint damage)
    {
        if (!dragonhowlOwners.TryGetValue(owner, out var state) || state.Plan is not null ||
            !IsOwnedDragonhowlBeforeCurrent(owner) || !state.Bindings.TryGetValue(binding.Connection, out var actual) ||
            !ReferenceEquals(actual, binding) || !state.Current.TryGetValue(binding.Connection, out var target) ||
            target.Character.MinHitPoints <= 0 || damage < target.Character.MinHitPoints ||
            calculated.Character.Uid != binding.Uid || calculated.AccountId != binding.AccountId ||
            !ReferenceEquals(calculated.NativeGameClient, binding.Client))
            throw new InvalidOperationException("ownedDragonhowlDeathRequiresActualPreparation");
        var token = new OwnedDragonhowlDeath(this);
        var data = new DragonhowlDeathState(state, binding, state.Current[state.Context.Connection], target, calculated);
        dragonhowlDeaths.Add(token, data);
        data.Death = WorldPacketHandlers.PrepareOwnedDragonhowlDeath(token);
        if (!token.IsCurrentPlain()) throw new InvalidOperationException("ownedDragonhowlDeathInputsChanged");
        return token;
    }

    private bool BindDragonhowlDeath(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
        OwnedDragonhowlDeath purpose)
    {
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var owner) || !ReferenceEquals(owner.Plan, plan) ||
            owner.NextPhase != index || !DragonhowlHeldCurrent(owner) ||
            !dragonhowlDeaths.TryGetValue(purpose, out var data) || !ReferenceEquals(data.Owner, owner) || data.Bound ||
            !ReferenceEquals(plan.Phases[index].Death, purpose)) return false;
        data.ExpectedActor = owner.Current[owner.Context.Connection];
        data.ExpectedTarget = owner.Current[data.Binding.Connection];
        data.Bound = true;
        return purpose.IsCurrentPlain();
    }
}
