using System.Runtime.CompilerServices;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlMonsterDeath { }
    internal sealed class OwnedDragonhowlDeathCps { }

    private sealed class DragonhowlMonsterDeathState(OwnedDragonhowlNonPlayer target,
        DragonhowlNonPlayerState source, MonsterDamageResult projected)
    {
        internal readonly OwnedDragonhowlNonPlayer Target = target;
        internal readonly DragonhowlNonPlayerState Source = source;
        internal readonly MonsterDamageResult Projected = projected;
        internal readonly List<OwnedDragonhowlDeathCps> Steps = [];
        internal string? WalletPath;
        internal byte[]? WalletBefore;
        internal bool Active;
        internal int NextStep;
    }

    private sealed class DragonhowlDeathCpsState(DragonhowlMonsterDeathState death,
        WorldPacketHandlers.DragonhowlDeathCpsData data)
    {
        internal readonly DragonhowlMonsterDeathState Death = death;
        internal readonly WorldPacketHandlers.DragonhowlDeathCpsData Data = data;
        internal WorldSession? Before, After;
        internal ActionPublication? Publication;
        internal bool Started, OutcomeConfirmed, OutputStarted, OutputComplete;
    }

    private readonly ConditionalWeakTable<OwnedDragonhowlMonsterDeath, DragonhowlMonsterDeathState> dragonhowlMonsterDeaths = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlNonPlayer, OwnedDragonhowlMonsterDeath> dragonhowlMonsterDeathTargets = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlDeathCps, DragonhowlDeathCpsState> dragonhowlDeathCps = new();

    internal OwnedDragonhowlMonsterDeath? PrepareDragonhowlMonsterDeath(OwnedDragonhowlOwner purpose,
        OwnedDragonhowlNonPlayer target)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null ||
            !IsOwnedDragonhowlBeforeCurrent(purpose) || !dragonhowlNonPlayers.TryGetValue(target, out var source) ||
            !ReferenceEquals(source.Owner, owner) || source.MonsterTarget is null || source.Monster is not { } monster ||
            source.Family is null || source.CalculatedActor is null || source.Result is not null ||
            source.Damage < monster.Hitpoints || monster.Hitpoints == 0 || !source.BeforeTargetCurrent() ||
            dragonhowlMonsterDeathTargets.TryGetValue(target, out _)) return null;
        // Lethality is derived from the registered, current incarnation and the
        // recorded calculation; a caller-supplied killed flag is never accepted.
        var projected = new MonsterDamageResult(monster.Hitpoints, 0, Killed: true,
            AlreadyDead: false, RequestedDamage: source.Damage);
        var token = new OwnedDragonhowlMonsterDeath();
        dragonhowlMonsterDeaths.Add(token, new(target, source, projected));
        dragonhowlMonsterDeathTargets.Add(target, token);
        return token;
    }

    internal bool TryPrepareDragonhowlDeathCps(OwnedDragonhowlMonsterDeath death, WorldSession projectedActor,
        WorldPacketHandlers.DragonhowlDeathCpsKind kind, out OwnedDragonhowlDeathCps? step, out WorldSession proposed)
    {
        step = null; proposed = projectedActor;
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var state) || state.Active || state.Source.Owner.Plan is not null ||
            state.Source.Owner.Started || state.Source.Owner.Failed || !DragonhowlHeldCurrent(state.Source.Owner) ||
            !state.Source.Owner.Frame.TryReadOwnedDragonhowl(state.Source.Owner.Request, out _) ||
            !state.Source.BeforeTargetCurrent() || !HasPreparedDragonhowlDropPhase(state) ||
            state.Steps.Count > 1 || (int)kind != state.Steps.Count) return false;
        var before = state.Source.CalculatedActor!;
        if (state.Steps.Count != 0)
        {
            if (!dragonhowlDeathCps.TryGetValue(state.Steps[^1], out var previous)) return false;
            before = previous.Data.After;
        }
        if (!ReferenceEquals(projectedActor, before)) return false;
        var data = WorldPacketHandlers.DragonhowlDeathCpsData.Prepare(before, state.Source.Monster!, state.Source.Family!, kind);
        if (data.Amount != 0 && !PrepareDragonhowlCpsFileData(state, data)) return false;
        step = new OwnedDragonhowlDeathCps();
        dragonhowlDeathCps.Add(step, new(state, data));
        state.Steps.Add(step); proposed = data.After;
        return true;
    }

    private bool PrepareDragonhowlCpsFileData(DragonhowlMonsterDeathState state, WorldPacketHandlers.DragonhowlDeathCpsData data)
    {
        var owner = state.Source.Owner;
        var binding = owner.Bindings[owner.Context.Connection];
        var actual = owner.Current[owner.Context.Connection];
        if (!DragonhowlHeldCurrent(owner) || !binding.Recovery.IsReadyForUsers(binding.UsersRoot) ||
            !binding.Recovery.TryRequireLiveJournalClear(out _) ||
            !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(actual, actual)) return false;
        string path = Path.Combine(binding.UsersRoot, actual.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        return SnapshotFileLockCoordinator.ExecuteLocked([path, owner.Frame.RequireDragonhowlGearPath(owner.Request)], () =>
        {
            if (!DragonhowlHeldCurrent(owner) || !owner.Frame.TryReadOwnedDragonhowl(owner.Request, out _) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedDragonhowlDeathCpsPreflight", out var before, out _) ||
                state.WalletBefore is { } captured && (!StringComparer.OrdinalIgnoreCase.Equals(path, state.WalletPath) ||
                    !before.AsSpan().SequenceEqual(captured))) return false;
            string text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before) || !HasSingleMonsterCpsField(text)) return false;
            var disk = CharacterSnapshotParser.ParseText(text);
            if (!LegacyCombatCharacterImageInputs.Matches(disk, actual.Character)) return false;
            var projected = ProjectAttributeCharacterText(text, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["ConquerPoints"] = data.After.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture) });
            var parsed = CharacterSnapshotParser.ParseText(projected);
            if (parsed.Uid != disk.Uid || CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed,
                disk with { ConquerPoints = data.After.Character.ConquerPoints })) return false;
            // Exercise the same concrete encoding before cost. This is DATA;
            // the later real writer still captures/revalidates its live image.
            _ = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
            state.WalletPath = path; state.WalletBefore ??= before;
            return true;
        });
    }

    internal bool IsPreparedDragonhowlMonsterDeathCurrent(OwnedDragonhowlMonsterDeath death)
    {
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var state) || state.Active || state.Steps.Count != 2 ||
            state.Source.Owner.Started || state.Source.Owner.Failed || !DragonhowlHeldCurrent(state.Source.Owner) ||
            !state.Source.BeforeTargetCurrent() || !HasPreparedDragonhowlDropPhase(state) || state.Steps.Any(token =>
                !dragonhowlDeathCps.TryGetValue(token, out var step) || !step.Data.InputsAreCurrentPlain())) return false;
        var owner = state.Source.Owner;
        if (state.WalletPath is not { } path || state.WalletBefore is not { } before)
            return state.Steps.All(token => dragonhowlDeathCps.TryGetValue(token, out var step) && step.Data.Amount == 0) &&
                owner.Frame.TryReadOwnedDragonhowl(owner.Request, out _);
        return SnapshotFileLockCoordinator.ExecuteLocked([path, owner.Frame.RequireDragonhowlGearPath(owner.Request)], () =>
            DragonhowlHeldCurrent(owner) && owner.Frame.TryReadOwnedDragonhowl(owner.Request, out _) &&
            CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedDragonhowlDeathCpsPreflightCurrent", out var image, out _) &&
            image.AsSpan().SequenceEqual(before));
    }

    internal bool TryBeginDragonhowlMonsterDeath(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death)
    {
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var state) || state.Active || state.Steps.Count != 2 ||
            !TryReadDragonhowlMonsterPublication(plan, state.Target, out _, out var original, out var family,
                out var after, out var result) || !result.Killed || result != state.Projected || after.Hitpoints != 0 ||
            !ReferenceEquals(original, state.Source.Monster) || !ReferenceEquals(family, state.Source.Family)) return false;
        var owner = state.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (state.Active || !DragonhowlDeathHeldCurrent(plan, state) || !HasPreparedDragonhowlDropPhase(state) ||
                state.Steps.Any(token => !dragonhowlDeathCps.TryGetValue(token, out var step) || !step.Data.InputsAreCurrentPlain()))
                return false;
            state.Active = true;
            return true;
        }
    }

    // This is a CP participant, not a completion of drops/map/mail/study/team XP.
    // The private full-death caller remains held until those real owners join.
    internal async ValueTask<bool> PublishDragonhowlDeathCpsAsync(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, CancellationToken cancellationToken)
    {
        if (!TryStartDragonhowlDeathCps(plan, death, step, out var state, out var context, out var before)) return false;
        if (state.Data.Amount != 0 &&
            (!TryCommitDragonhowlDeathCps(context, before, step, out _, out var publication) || publication is null)) return false;
        return await state.Data.PublishAsync(plan, death, step, cancellationToken).ConfigureAwait(false);
    }

    private bool TryStartDragonhowlDeathCps(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, out DragonhowlDeathCpsState state,
        out WorldPacketHandlerContext context, out WorldSession before)
    {
        state = null!; context = null!; before = null!;
        if (!TryReadDragonhowlDeathCps(plan, death, step, out var actual)) return false;
        var owner = actual.Death.Source.Owner;
        RefuseBoundSpellReentry(owner.Context.Connection);
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (actual.Started || !DragonhowlDeathHeldCurrent(plan, actual.Death) ||
                actual.Death.NextStep == 0 && !HasCompletedDragonhowlDropPhase(plan, actual.Death) ||
                !actual.Data.MatchesBefore(owner.Current[owner.Context.Connection])) return false;
            before = owner.Current[owner.Context.Connection];
            actual.Before = before; actual.Started = true;
            if (actual.Data.Amount == 0)
            {
                // A sampled miss/no applicable boss policy publishes no write
                // receipt and grants no wallet mutation.
                actual.After = before; actual.OutcomeConfirmed = true;
            }
            context = owner.Context with { Session = before }; state = actual;
            return true;
        }
    }

    private bool TryReadDragonhowlDeathCps(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, out DragonhowlDeathCpsState state)
    {
        state = null!;
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var lineage) || !lineage.Active ||
            !ReferenceEquals(lineage.Source.Owner.Plan, plan) || !dragonhowlDeathCps.TryGetValue(step, out var actual) ||
            !ReferenceEquals(actual.Death, lineage) || lineage.NextStep >= lineage.Steps.Count ||
            !ReferenceEquals(lineage.Steps[lineage.NextStep], step)) return false;
        state = actual; return true;
    }

    private bool DragonhowlDeathHeldCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan, DragonhowlMonsterDeathState state)
    {
        var source = state.Source; var owner = source.Owner;
        return ReferenceEquals(owner.Plan, plan) && !owner.Failed && !owner.Completed && owner.Started && owner.Committed &&
            source.Result is MonsterDamageResult actual && actual == state.Projected && actual.Killed &&
            source.Phase >= 0 && source.Phase == owner.NextPhase - 1 &&
            ReferenceEquals(plan.Phases[source.Phase].NonPlayer, state.Target) &&
            DragonhowlHeldCurrent(owner) && source.CommittedTargetCurrent() && plan.InputsAreCurrentPlain() &&
            owner.Attempt is { } attempt && owner.Frame.IsDragonhowlActive(owner.Request, attempt,
                owner.Current[owner.Context.Connection], DragonhowlCurrentPrimary(owner));
    }

    private bool TryEnterDragonhowlCpsWriter(OwnedDragonhowlDeathCps step, WorldPacketHandlerContext context,
        WorldSession expected, uint amount, out IDisposable? scope)
    {
        scope = null;
        if (!dragonhowlDeathCps.TryGetValue(step, out var state) || state.Death.Source.Owner.Plan is not { } plan ||
            !Monitor.IsEntered(state.Death.Source.Owner.Frame.RequireActionLifecycleGate(this))) return false;
        var owner = state.Death.Source.Owner;
        var held = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
        try
        {
            if (!state.Started || state.OutcomeConfirmed || state.Data.Amount == 0 || state.Data.Amount != amount ||
                !ReferenceEquals(state.Before, expected) || !ReferenceEquals(context.Connection, owner.Context.Connection) ||
                !ReferenceEquals(context.FrameOperationLease, owner.Context.FrameOperationLease) ||
                !ReferenceEquals(owner.Current[owner.Context.Connection], expected) || state.Death.NextStep >= state.Death.Steps.Count ||
                !ReferenceEquals(state.Death.Steps[state.Death.NextStep], step) ||
                !DragonhowlDeathHeldCurrent(plan, state.Death) || !state.Data.MatchesBefore(expected)) return false;
            scope = held; return true;
        }
        finally { if (scope is null) held.Dispose(); }
    }

    private string[] DragonhowlCpsFilePaths(OwnedDragonhowlDeathCps step, string characterPath)
    {
        if (!dragonhowlDeathCps.TryGetValue(step, out var state)) throw InvalidSpellPublication();
        var owner = state.Death.Source.Owner;
        return [characterPath, owner.Frame.RequireDragonhowlGearPath(owner.Request)];
    }

    private bool PrepareDragonhowlCpsPublication(OwnedDragonhowlDeathCps step, WorldSession expected,
        WorldSession final, ActionPublication publication)
    {
        if (!dragonhowlDeathCps.TryGetValue(step, out var state) || state.Publication is not null || state.OutcomeConfirmed ||
            !ReferenceEquals(state.Before, expected) || state.Death.Source.Owner.Plan is not { } plan ||
            !DragonhowlDeathHeldCurrent(plan, state.Death) || !state.Data.MatchesAfter(final) ||
            !ReferenceEquals(publication.Published, final)) return false;
        state.Publication = publication; state.After = final;
        return true;
    }

    private bool DragonhowlCpsPreparedCurrent(OwnedDragonhowlDeathCps step, WorldSession expected,
        WorldSession final, ActionPublication publication) =>
        dragonhowlDeathCps.TryGetValue(step, out var state) && !state.OutcomeConfirmed &&
        ReferenceEquals(state.Publication, publication) && ReferenceEquals(state.Before, expected) &&
        ReferenceEquals(state.After, final) && state.Death.Source.Owner.Plan is { } plan &&
        DragonhowlDeathHeldCurrent(plan, state.Death) && state.Data.MatchesBefore(expected) && state.Data.MatchesAfter(final);

    private void ConfirmDragonhowlCpsPublication(OwnedDragonhowlDeathCps step, WorldSession expected,
        WorldSession final, ActionPublication publication)
    {
        if (!dragonhowlDeathCps.TryGetValue(step, out var state) || state.OutcomeConfirmed ||
            !ReferenceEquals(state.Publication, publication) || !ReferenceEquals(state.Before, expected) ||
            !ReferenceEquals(state.After, final) || !ReferenceEquals(state.Death.Source.Owner.Current[state.Death.Source.Owner.Context.Connection], expected) ||
            !publication.TryCurrent(out var actual) || !ReferenceEquals(actual, final))
            throw new InvalidOperationException("ownedDragonhowlDeathCpsCommittedOwnerChanged");
        // Exact real writer successor retained before any observer can throw.
        var owner = state.Death.Source.Owner;
        owner.Current[owner.Context.Connection] = final;
        state.OutcomeConfirmed = true;
        if (owner.Plan is not { } plan || !DragonhowlDeathHeldCurrent(plan, state.Death))
            throw new InvalidOperationException("ownedDragonhowlDeathCpsCommittedParticipantsChanged");
    }

    internal bool TryBeginDragonhowlDeathCpsOutput(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, WorldPacketHandlers.DragonhowlDeathCpsData data,
        out WorldSession current)
    {
        current = null!;
        if (!TryReadDragonhowlDeathCps(plan, death, step, out var state)) return false;
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (!ReferenceEquals(state.Data, data) || state.OutputStarted || !DragonhowlDeathCpsOutputHeldCurrent(plan, state)) return false;
            state.OutputStarted = true; current = state.After!; return true;
        }
    }

    internal bool IsDragonhowlDeathCpsOutputCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, WorldSession expected)
    {
        if (!TryReadDragonhowlDeathCps(plan, death, step, out var state)) return false;
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            return state.OutputStarted && !state.OutputComplete && ReferenceEquals(state.After, expected) &&
                DragonhowlDeathCpsOutputHeldCurrent(plan, state);
        }
    }

    internal bool CompleteDragonhowlDeathCpsOutput(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlDeathCps step, WorldSession expected)
    {
        if (!TryReadDragonhowlDeathCps(plan, death, step, out var state)) return false;
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (!state.OutputStarted || state.OutputComplete || !ReferenceEquals(state.After, expected) ||
                !DragonhowlDeathCpsOutputHeldCurrent(plan, state)) return false;
            state.OutputComplete = true; state.Death.NextStep++;
            return true;
        }
    }

    private bool DragonhowlDeathCpsOutputHeldCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan, DragonhowlDeathCpsState state) =>
        state.Started && state.OutcomeConfirmed && state.After is { } after && DragonhowlDeathHeldCurrent(plan, state.Death) &&
        ReferenceEquals(state.Death.Source.Owner.Current[state.Death.Source.Owner.Context.Connection], after) &&
        state.Data.MatchesAfter(after) && (state.Data.Amount == 0 ? state.Publication is null :
            state.Publication is { } publication && publication.TryCurrent(out var current) && ReferenceEquals(current, after));
}
