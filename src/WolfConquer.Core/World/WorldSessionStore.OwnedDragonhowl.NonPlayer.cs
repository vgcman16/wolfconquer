using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class OwnedDragonhowlNonPlayer
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlNonPlayer(WorldSessionStore store) => this.store = store;
        internal bool TryReadMonsterBefore(MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget target,
            out LegacyMonsterInstance expected, out uint damage, out DateTimeOffset now, out WorldSession actor)
        {
            expected = null!; damage = 0; now = default; actor = null!;
            if (!store.TryReadDragonhowlNonPlayerBefore(this, out var data) || !ReferenceEquals(data.MonsterTarget, target) ||
                data.Monster is not { } actual) return false;
            expected = actual; damage = data.Damage; now = data.Now; actor = data.Owner.Current[data.Owner.Context.Connection];
            return true;
        }
        internal bool TryReadSobBefore(SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget target,
            out SobNpc expected, out uint damage, out DateTimeOffset now, out LegacySobNpcDeathBehavior behavior)
        {
            expected = null!; damage = 0; now = default; behavior = default;
            if (!store.TryReadDragonhowlNonPlayerBefore(this, out var data) || !ReferenceEquals(data.SobTarget, target) ||
                data.Sob is not { } actual) return false;
            expected = actual; damage = data.Damage; now = data.Now; behavior = data.DeathBehavior;
            return true;
        }

        internal bool InputsAreCurrentPlain() => store.dragonhowlNonPlayers.TryGetValue(this, out var data) &&
            (data.Result is not null ? data.CommittedTargetCurrent() : data.BeforeTargetCurrent()) &&
            (data.SobEvent is null || data.SobEvent.IsCurrentPlain());
        internal bool TryProjectSob(out SobNpc after, out SobNpcDamageResult result)
        {
            after = null!; result = default;
            return store.dragonhowlNonPlayers.TryGetValue(this, out var data) && data.Result is null &&
                data.SobTarget is { } target && target.TryProject(data.Damage, data.DeathBehavior, out after, out result);
        }
        internal bool CoversSobEventPreparation(WorldSession calculatedActor, SobNpc original,
            SobNpc projectedAfter, SobNpcDamageResult projectedResult) =>
            store.dragonhowlNonPlayers.TryGetValue(this, out var data) && data.Owner.Plan is null &&
            !data.Owner.Started && !data.Owner.Failed && data.Result is null &&
            ReferenceEquals(data.CalculatedActor, calculatedActor) && ReferenceEquals(data.Sob, original) &&
            store.DragonhowlHeldCurrent(data.Owner) && data.Owner.Frame.TryReadOwnedDragonhowl(data.Owner.Request, out _) &&
            data.SobTarget is { } target && target.TryProject(data.Damage, data.DeathBehavior, out var after, out var result) &&
            after == projectedAfter && result == projectedResult;
    }

    private sealed partial class DragonhowlNonPlayerState
    {
        internal readonly DragonhowlOwnerState Owner;
        internal readonly MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget? MonsterTarget;
        internal readonly SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget? SobTarget;
        internal readonly LegacyMonsterInstance? Monster;
        internal readonly Monster? Family;
        internal readonly SobNpc? Sob;
        internal readonly WorldSession? CalculatedActor;
        internal readonly uint Damage;
        internal readonly DateTimeOffset Now;
        internal readonly LegacySobNpcDeathBehavior DeathBehavior;
        internal object? Result;
        internal int Phase = -1;
        internal bool OutputStarted;

        internal DragonhowlNonPlayerState(DragonhowlOwnerState owner,
            MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget target, LegacyMonsterInstance monster, Monster family,
            uint damage, DateTimeOffset now, WorldSession calculatedActor)
        { Owner = owner; MonsterTarget = target; Monster = monster; Family = family; Damage = damage; Now = now; CalculatedActor = calculatedActor; }
        internal DragonhowlNonPlayerState(DragonhowlOwnerState owner,
            SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget target, SobNpc sob, uint damage, DateTimeOffset now,
            LegacySobNpcDeathBehavior behavior, WorldSession calculatedActor)
        { Owner = owner; SobTarget = target; Sob = sob; Damage = damage; Now = now; DeathBehavior = behavior; CalculatedActor = calculatedActor; }
        internal bool BeforeTargetCurrent() => MonsterTarget is not null
            ? MonsterTarget.TryReadCurrent(out var current, out var family) && current == Monster && ReferenceEquals(family, Family)
            : SobTarget is not null && SobTarget.TryReadCurrent(out var currentSob) && currentSob == Sob;
        internal bool CommittedTargetCurrent() => MonsterTarget?.IsCommittedCurrent() ?? (SobTarget?.IsCommittedCurrent() == true);
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedDragonhowlNonPlayer, DragonhowlNonPlayerState>
        dragonhowlNonPlayers = new();

    internal OwnedDragonhowlNonPlayer? PrepareDragonhowlMonster(OwnedDragonhowlOwner purpose, WorldSession calculatedActor,
        LegacyMonsterInstance monster, Monster family, uint damage)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null || !IsOwnedDragonhowlBeforeCurrent(purpose) ||
            owner.Context.MonsterStates is not { } monsters || owner.Context.WorldData is not { } catalog ||
            calculatedActor.AccountId != owner.Context.Session!.AccountId ||
            calculatedActor.Character.Uid != owner.Context.Session.Character.Uid ||
            !ReferenceEquals(calculatedActor.NativeGameClient, owner.Context.Session.NativeGameClient) ||
            owner.Frame.CaptureDragonhowlSelectedMonster(owner.Request, monster, family) is not { } target ||
            !target.TryReadCurrent(out var actual, out var actualFamily) || actual != monster || !ReferenceEquals(actualFamily, family)) return null;
        var token = new OwnedDragonhowlNonPlayer(this);
        dragonhowlNonPlayers.Add(token, new(owner, target, monster, family, damage,
            (owner.Context.TimeProvider ?? throw InvalidSpellPublication()).GetUtcNow(), calculatedActor));
        return token;
    }

    internal OwnedDragonhowlNonPlayer? PrepareDragonhowlSob(OwnedDragonhowlOwner purpose, WorldSession calculatedActor,
        OwnedDragonhowlSobSelection selection,
        uint damage, LegacySobNpcDeathBehavior behavior)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null || !IsOwnedDragonhowlBeforeCurrent(purpose) ||
            owner.Context.SobNpcStates is not { } sobs || owner.Context.WorldData is not { } catalog ||
            calculatedActor.AccountId != owner.Context.Session!.AccountId ||
            calculatedActor.Character.Uid != owner.Context.Session.Character.Uid ||
            !ReferenceEquals(calculatedActor.NativeGameClient, owner.Context.Session.NativeGameClient) ||
            !dragonhowlSobSelections.TryGetValue(selection, out var selected) || !ReferenceEquals(selected.Owner, owner) ||
            !selected.Target.TryReadCurrent(out var actual) || actual != selected.Current) return null;
        var target = selected.Target;
        var sob = selected.Current;
        var token = new OwnedDragonhowlNonPlayer(this);
        dragonhowlNonPlayers.Add(token, new(owner, target, sob, damage,
            (owner.Context.TimeProvider ?? throw InvalidSpellPublication()).GetUtcNow(), behavior, calculatedActor));
        return token;
    }

    private bool TryReadDragonhowlNonPlayerBefore(OwnedDragonhowlNonPlayer token, out DragonhowlNonPlayerState data)
    {
        data = null!;
        if (!dragonhowlNonPlayers.TryGetValue(token, out var actual) || actual.Result is not null || actual.Owner.Plan is not { } plan ||
            actual.Owner.Failed || !actual.Owner.Started || actual.Owner.Attempt is not { } attempt ||
            actual.Phase < 0 || actual.Owner.NextPhase != actual.Phase || actual.Phase >= plan.Phases.Count ||
            !ReferenceEquals(plan.Phases[actual.Phase].NonPlayer, token) || !DragonhowlHeldCurrent(actual.Owner) ||
            !(actual.SobEventHeld is { } held ? held.IsCurrentPlain() :
                actual.Owner.Frame.IsDragonhowlActive(actual.Owner.Request, attempt,
                    actual.Owner.Current[actual.Owner.Context.Connection], DragonhowlCurrentPrimary(actual.Owner)))) return false;
        data = actual; return true;
    }

    private bool TryCommitDragonhowlNonPlayerHeld(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int phase,
        OwnedDragonhowlNonPlayer token)
    {
        if (!dragonhowlNonPlayers.TryGetValue(token, out var data) || !ReferenceEquals(data.Owner.Plan, plan) ||
            data.Phase >= 0 || !DragonhowlHeldCurrent(data.Owner) || !data.BeforeTargetCurrent()) return false;
        data.Phase = phase;
        if (!TryReadDragonhowlNonPlayerBefore(token, out _)) return false;
        if (data.SobEvent is not null) return TryCommitDragonhowlSobEventHeld(plan, token, data);
        // Denial-only uncertainty boundary: a real store mutation may throw
        // after changing HP/ranking. Never classify that as clean rejection.
        data.Owner.Committed = true;
        if (data.MonsterTarget is { } monster)
        {
            if (!monster.TryCommit(token, out var result) || result.AlreadyDead || result.RequestedDamage != data.Damage ||
                result.DamageApplied != Math.Min(data.Damage, data.Monster!.Hitpoints)) return false;
            data.Result = result;
        }
        else if (data.SobTarget is { } sob)
        {
            if (!sob.TryCommit(token, out var result) || result.AlreadyDead ||
                result.DamageApplied != Math.Min(data.Damage, sob.Hitpoints)) return false;
            data.Result = result;
        }
        else return false;
        return data.CommittedTargetCurrent();
    }

    internal bool TryReadDragonhowlNonPlayerPublication(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, out WorldSession actor, out MonsterDamageResult? monster, out SobNpcDamageResult? sob)
    {
        actor = null!; monster = null; sob = null;
        if (!dragonhowlNonPlayers.TryGetValue(token, out var data) || !ReferenceEquals(data.Owner.Plan, plan) || data.Owner.Failed ||
            data.Result is null || data.Phase < 0 || data.Phase >= data.Owner.NextPhase) return false;
        lock (data.Owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(data.Owner));
            if (!DragonhowlHeldCurrent(data.Owner) || !data.CommittedTargetCurrent() || data.Owner.Attempt is not { } attempt ||
                !data.Owner.Frame.IsDragonhowlActive(data.Owner.Request, attempt,
                    data.Owner.Current[data.Owner.Context.Connection], DragonhowlCurrentPrimary(data.Owner))) return false;
            actor = data.Owner.Current[data.Owner.Context.Connection];
            if (data.Result is MonsterDamageResult monsterResult) monster = monsterResult;
            else if (data.Result is SobNpcDamageResult sobResult) sob = sobResult;
            else return false;
            return true;
        }
    }

    internal bool TryReadDragonhowlSobPublication(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, out WorldSession actor, out SobNpc original, out SobNpc after, out SobNpcDamageResult result)
    {
        actor = null!; original = null!; after = null!; result = default;
        if (!TryReadDragonhowlNonPlayerPublication(plan, token, out actor, out _, out var actualResult) || actualResult is null ||
            !dragonhowlNonPlayers.TryGetValue(token, out var data) || data.Sob is not { } source || data.SobTarget is not { } target ||
            !target.TryReadCommitted(out after, out result) || result != actualResult.Value) return false;
        original = source;
        return true;
    }

    internal bool TryReadDragonhowlMonsterPublication(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, out WorldSession actor, out LegacyMonsterInstance original, out Monster family,
        out LegacyMonsterInstance after, out MonsterDamageResult result)
    {
        actor = null!; original = null!; family = null!; after = null!; result = default;
        if (!TryReadDragonhowlNonPlayerPublication(plan, token, out actor, out var actualResult, out _) || actualResult is null ||
            !dragonhowlNonPlayers.TryGetValue(token, out var data) || data.Monster is not { } source || data.Family is not { } actualFamily ||
            data.MonsterTarget is not { } target || !target.TryReadCommitted(out after, out result) || result != actualResult.Value) return false;
        original = source; family = actualFamily; return true;
    }

    internal bool TryBeginDragonhowlNonPlayerOutput(WorldPacketHandlers.OwnedDragonhowlPreparation plan, OwnedDragonhowlNonPlayer token)
    {
        if (!dragonhowlNonPlayers.TryGetValue(token, out var data) || !ReferenceEquals(data.Owner.Plan, plan)) return false;
        lock (data.Owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(data.Owner));
            if (data.OutputStarted || data.Result is null || data.Owner.Failed || data.Phase < 0 ||
                data.Phase != data.Owner.NextPhase - 1 || !DragonhowlHeldCurrent(data.Owner) || !data.CommittedTargetCurrent() ||
                data.Owner.Attempt is not { } attempt || !data.Owner.Frame.IsDragonhowlActive(data.Owner.Request, attempt,
                    data.Owner.Current[data.Owner.Context.Connection], DragonhowlCurrentPrimary(data.Owner))) return false;
            data.OutputStarted = true;
            return true;
        }
    }
}
