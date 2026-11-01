using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlMonsterDrops : IDisposable
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlMonsterDrops(WorldSessionStore store) => this.store = store;
        public void Dispose() => store.DisposeDragonhowlDrops(this);
    }
    internal sealed class OwnedDragonhowlMonsterDropReceipt { }
    internal sealed class OwnedDragonhowlDropQuality
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlDropQuality(WorldSessionStore store) => this.store = store;
        internal bool TryPrepare(out byte quality) => store.TryPrepareDragonhowlDropQuality(this, out quality);
        internal bool TryReadPreparation(MonsterRuntimeStateStore monsters, out int family,
            out MonsterRuntimeStateStore.PreparedDragonhowlDropQuality? previous) =>
            store.TryReadDragonhowlDropQualityPreparation(this, monsters, out family, out previous);
        internal bool TryReadCommit(MonsterRuntimeStateStore.PreparedDragonhowlDropQuality prepared) =>
            store.TryReadDragonhowlDropQualityCommit(this, prepared);
    }
    internal sealed class OwnedDragonhowlGroundAttempt
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlGroundAttempt(WorldSessionStore store) => this.store = store;
        internal bool TryReadPreparation(FloorItemRuntimeStateStore floor,
            out WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data,
            out LegacyGameMapTerrainQuery terrain, out DateTimeOffset now,
            out FloorItemRuntimeStateStore.PreparedDragonhowlGround? previous) =>
            store.TryReadDragonhowlGroundPreparation(this, floor, out data, out terrain, out now, out previous);
        internal bool TryReadCommit(FloorItemRuntimeStateStore.PreparedDragonhowlGround prepared, out DateTimeOffset now) =>
            store.TryReadDragonhowlGroundCommit(this, prepared, out now);
    }

    private sealed class DragonhowlDropsState(DragonhowlMonsterDeathState death, WorldPacketHandlers.DragonhowlDropRecipe recipe)
    {
        internal readonly DragonhowlMonsterDeathState Death = death;
        internal readonly WorldPacketHandlers.DragonhowlDropRecipe Recipe = recipe;
        internal readonly List<OwnedDragonhowlGroundAttempt> Attempts = [];
        internal readonly List<DragonhowlDropQualityState> Qualities = [];
        internal int PreparingIndex = -1;
        internal bool Prepared, Started, Disposed;
        internal int Next;
        internal WorldSession? Actor;
        internal OwnedDragonhowlMonsterDropReceipt? Receipt;
    }
    private sealed class DragonhowlDropQualityState(DragonhowlDropsState drops, int index)
    {
        internal readonly DragonhowlDropsState Drops = drops;
        internal readonly int Index = index;
        internal MonsterRuntimeStateStore.PreparedDragonhowlDropQuality? Prepared;
        internal bool Committed;
    }
    private sealed class DragonhowlGroundAttemptState(DragonhowlDropsState drops,
        WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data, int index)
    {
        internal readonly DragonhowlDropsState Drops = drops;
        internal readonly WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData Data = data;
        internal readonly int Index = index;
        internal FloorItemRuntimeStateStore.PreparedDragonhowlGround? Ground;
        internal OwnedDragonhowlDropQuality? Quality;
        internal bool Committed, OutputCompleted;
    }
    private readonly ConditionalWeakTable<OwnedDragonhowlMonsterDrops, DragonhowlDropsState> dragonhowlDrops = new();
    private readonly ConditionalWeakTable<DragonhowlMonsterDeathState, DragonhowlDropsState> dragonhowlDropsByDeath = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlGroundAttempt, DragonhowlGroundAttemptState> dragonhowlGroundAttempts = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlMonsterDropReceipt, DragonhowlDropsState> dragonhowlDropReceipts = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlDropQuality, DragonhowlDropQualityState> dragonhowlDropQualities = new();
    private readonly ConditionalWeakTable<DragonhowlOwnerState, Dictionary<int, MonsterRuntimeStateStore.PreparedDragonhowlDropQuality>> dragonhowlQualityTails = new();
    // Only this actual registered proc's local floor planning sequence. No
    // entry is reserved in the live floor store until its killed commit phase.
    private readonly ConditionalWeakTable<DragonhowlOwnerState,
        Dictionary<FloorItemRuntimeStateStore, FloorItemRuntimeStateStore.PreparedDragonhowlGround>> dragonhowlGroundTails = new();

    private bool TryPrepareDragonhowlDropQuality(OwnedDragonhowlDropQuality token, out byte quality)
    {
        quality = 0;
        if (!dragonhowlDropQualities.TryGetValue(token, out var state) || state.Prepared is not null ||
            state.Drops.Recipe.Context.MonsterStates is not { } monsters) return false;
        var prepared = MonsterRuntimeStateStore.PreparedDragonhowlDropQuality.TryPrepare(monsters, token);
        if (prepared is null) return false;
        var tails = dragonhowlQualityTails.GetOrCreateValue(state.Drops.Death.Source.Owner);
        tails[state.Drops.Death.Source.Family!.Id] = prepared;
        state.Prepared = prepared; quality = prepared.Quality; return true;
    }

    private bool TryReadDragonhowlDropQualityPreparation(OwnedDragonhowlDropQuality token,
        MonsterRuntimeStateStore monsters, out int family, out MonsterRuntimeStateStore.PreparedDragonhowlDropQuality? previous)
    {
        family = 0; previous = null;
        if (!dragonhowlDropQualities.TryGetValue(token, out var state) || state.Prepared is not null ||
            state.Index != state.Drops.PreparingIndex || state.Drops.Prepared || state.Drops.Started || state.Drops.Disposed ||
            state.Drops.Death.Source.Owner.Started || state.Drops.Death.Source.Owner.Failed ||
            !DragonhowlHeldCurrent(state.Drops.Death.Source.Owner) || !state.Drops.Death.Source.BeforeTargetCurrent() ||
            !ReferenceEquals(monsters, state.Drops.Recipe.Context.MonsterStates) || !state.Drops.Recipe.InputsAreCurrentPlain()) return false;
        family = state.Drops.Death.Source.Family!.Id;
        if (dragonhowlQualityTails.TryGetValue(state.Drops.Death.Source.Owner, out var tails)) tails.TryGetValue(family, out previous);
        return true;
    }

    private bool TryReadDragonhowlDropQualityCommit(OwnedDragonhowlDropQuality token,
        MonsterRuntimeStateStore.PreparedDragonhowlDropQuality prepared)
    {
        return dragonhowlDropQualities.TryGetValue(token, out var state) && ReferenceEquals(state.Prepared, prepared) &&
            !state.Committed && state.Index == state.Drops.Next && state.Drops.Started && !state.Drops.Disposed &&
            state.Drops.Death.Source.Owner.Plan is { } plan && DragonhowlDropPhaseHeldCurrent(plan, state.Drops);
    }

    private static bool DragonhowlDropQualityCurrent(DragonhowlDropsState state) =>
        state.Qualities.LastOrDefault(row => row.Prepared is not null)?.Prepared?.IsSequenceCurrent() ?? true;

    internal bool TryPrepareDragonhowlMonsterDrops(OwnedDragonhowlMonsterDeath death,
        out OwnedDragonhowlMonsterDrops? prepared, out string reason)
    {
        prepared = null; reason = "ownedDragonhowlDropDeathUnavailable";
        if (!dragonhowlMonsterDeaths.TryGetValue(death, out var lineage) || lineage.Active || lineage.Source.Owner.Started ||
            lineage.Source.Owner.Plan is not null || lineage.Source.Owner.Failed || !DragonhowlHeldCurrent(lineage.Source.Owner) ||
            !lineage.Source.BeforeTargetCurrent() || dragonhowlDropsByDeath.TryGetValue(lineage, out _) ||
            !WorldPacketHandlers.DragonhowlDropRecipe.TryPrepare(lineage.Source.Owner.Context, lineage.Source.CalculatedActor!,
                lineage.Source.Monster!, lineage.Source.Family!, out var recipe, out reason) || recipe is null) return false;
        var token = new OwnedDragonhowlMonsterDrops(this);
        var state = new DragonhowlDropsState(lineage, recipe);
        dragonhowlDrops.Add(token, state); dragonhowlDropsByDeath.Add(lineage, state);
        try
        {
            ushort x = recipe.Monster.X, y = recipe.Monster.Y;
            for (int index = 0; index < recipe.Count; index++)
            {
                state.PreparingIndex = index;
                var quality = new OwnedDragonhowlDropQuality(this);
                var qualityState = new DragonhowlDropQualityState(state, index);
                dragonhowlDropQualities.Add(quality, qualityState); state.Qualities.Add(qualityState);
                if (!recipe.TryCreateAttempt(index, x, y, quality, out var data, out reason) || data is null) return false;
                var attempt = new OwnedDragonhowlGroundAttempt(this);
                var attemptState = new DragonhowlGroundAttemptState(state, data, index) { Quality = quality };
                dragonhowlGroundAttempts.Add(attempt, attemptState); state.Attempts.Add(attempt);
                if (data.SelectionAbsent) continue;
                if (recipe.Context.FloorItems is not { } floor)
                { reason = "ownedDragonhowlFloorRuntimeUnavailable"; return false; }
                if (!floor.TryPrepareDragonhowlGround(attempt, out var ground, out reason) || ground is null) return false;
                attemptState.Ground = ground;
                dragonhowlGroundTails.GetOrCreateValue(lineage.Source.Owner)[floor] = ground;
                if (data.Special) continue; // Special bundles restart from the real monster cell.
                if (ground.HasPlannedGround) { x = ground.PlannedX; y = ground.PlannedY; }
                // A rejected money pile retains the last valid origin. Resetting
                // it to (0,0) can move later piles to unrelated walkable cells.
                else if (!data.IsMoney) break; // Ordinary item failure still stops.
            }
            state.PreparingIndex = -1;
            state.Prepared = true;
            if (!IsPreparedDragonhowlMonsterDropsCurrent(token)) return false;
            prepared = token; reason = string.Empty; return true;
        }
        finally { if (prepared is null) token.Dispose(); }
    }

    internal bool IsPreparedDragonhowlMonsterDropsCurrent(OwnedDragonhowlMonsterDrops token)
    {
        if (!dragonhowlDrops.TryGetValue(token, out var state) || !state.Prepared || state.Started || state.Disposed ||
            state.Death.Active || state.Death.Source.Owner.Started || state.Death.Source.Owner.Failed ||
            !DragonhowlHeldCurrent(state.Death.Source.Owner) || !state.Death.Source.BeforeTargetCurrent() ||
            !state.Recipe.InputsAreCurrentPlain() || !DragonhowlDropQualityCurrent(state)) return false;
        return state.Attempts.All(attempt => dragonhowlGroundAttempts.TryGetValue(attempt, out var data) &&
            (data.Data.SelectionAbsent || DropOwnersCurrent(state) && data.Ground?.InputsAreCurrentPlain() == true));
    }

    private bool TryReadDragonhowlGroundPreparation(OwnedDragonhowlGroundAttempt token, FloorItemRuntimeStateStore floor,
        out WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data,
        out LegacyGameMapTerrainQuery terrain, out DateTimeOffset now,
        out FloorItemRuntimeStateStore.PreparedDragonhowlGround? previous)
    {
        data = null!; terrain = null!; now = default; previous = null;
        if (!dragonhowlGroundAttempts.TryGetValue(token, out var state) || state.Ground is not null || state.Drops.Prepared ||
            state.Drops.Started || state.Drops.Disposed || state.Drops.Death.Source.Owner.Started ||
            state.Drops.Death.Source.Owner.Failed || !DragonhowlHeldCurrent(state.Drops.Death.Source.Owner) ||
            !state.Drops.Death.Source.BeforeTargetCurrent() || !ReferenceEquals(state.Drops.Recipe.Context.FloorItems, floor) ||
            state.Drops.Recipe.Context.Terrain is not { } actual || !DropOwnersCurrent(state.Drops) ||
            !state.Data.Recipe.InputsAreCurrentPlain()) return false;
        data = state.Data; terrain = actual;
        now = (state.Drops.Recipe.Context.TimeProvider ?? throw InvalidSpellPublication()).GetUtcNow();
        if (dragonhowlGroundTails.TryGetValue(state.Drops.Death.Source.Owner, out var tails))
            tails.TryGetValue(floor, out previous);
        return true;
    }

    private bool DropOwnersCurrent(DragonhowlDropsState state) => state.Recipe.Context.FloorItems is { } floor &&
        state.Recipe.Context.Terrain is { } terrain && state.Death.Source.Owner.Frame.AreDragonhowlDropOwnersCurrent(
            state.Death.Source.Owner.Request, floor, terrain);

    private bool TryReadDragonhowlGroundCommit(OwnedDragonhowlGroundAttempt token,
        FloorItemRuntimeStateStore.PreparedDragonhowlGround prepared, out DateTimeOffset now)
    {
        now = default;
        if (!dragonhowlGroundAttempts.TryGetValue(token, out var state) || !ReferenceEquals(state.Ground, prepared) ||
            state.Committed || state.Drops.Next != state.Index || !state.Drops.Started || state.Drops.Disposed ||
            state.Drops.Death.Source.Owner.Plan is not { } plan || !DragonhowlDropPhaseHeldCurrent(plan, state.Drops)) return false;
        now = (state.Drops.Recipe.Context.TimeProvider ?? throw InvalidSpellPublication()).GetUtcNow(); return true;
    }

    internal async ValueTask<bool> PublishDragonhowlMonsterDropsAsync(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlMonsterDeath death, OwnedDragonhowlMonsterDrops token, CancellationToken cancellationToken)
    {
        if (!dragonhowlDrops.TryGetValue(token, out var state) || !dragonhowlMonsterDeaths.TryGetValue(death, out var lineage) ||
            !ReferenceEquals(state.Death, lineage) || !lineage.Active || !state.Prepared) return false;
        var owner = lineage.Source.Owner;
        RefuseBoundSpellReentry(owner.Context.Connection);
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (state.Started || state.Disposed || !DragonhowlDeathHeldCurrent(plan, lineage) ||
                !state.Recipe.MatchesActor(owner.Current[owner.Context.Connection]) ||
                state.Attempts.Any(attempt => dragonhowlGroundAttempts.TryGetValue(attempt, out var row) && !row.Data.SelectionAbsent) &&
                    !DropOwnersCurrent(state)) return false;
            state.Actor = owner.Current[owner.Context.Connection]; state.Started = true;
        }
        foreach (var attempt in state.Attempts)
        {
            FloorItemRuntimeStateStore.PreparedDragonhowlGround? ground;
            lock (owner.Frame.RequireActionLifecycleGate(this))
            {
                using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
                var step = dragonhowlGroundAttempts.TryGetValue(attempt, out var actual) ? actual : throw InvalidSpellPublication();
                if (!DragonhowlDropPhaseHeldCurrent(plan, state) || step.Quality is not { } quality ||
                    !dragonhowlDropQualities.TryGetValue(quality, out var qualityState)) return false;
                if (qualityState.Prepared is { } counter && !counter.TryCommit(quality)) return false;
                qualityState.Committed = true;
                ground = step.Ground;
                if (!step.Data.SelectionAbsent && (ground is null || !ground.TryCommit(attempt))) return false;
                // The real floor result is retained before any socket can throw.
                step.Committed = true; state.Next++;
            }
            if (ground?.Entry is { } entry)
            {
                byte[] frame = ground.CopyFrame();
                if (LegacyFloorItemVisibility.CanBroadcast(state.Actor!.Character, entry))
                    await SendDragonhowlDropFrameAsync(plan, state, ground, owner.Context.Connection, frame, cancellationToken).ConfigureAwait(false);
                foreach (var peer in Snapshot())
                {
                    if (ReferenceEquals(peer.Connection, owner.Context.Connection) || peer.Session.Character.Uid == state.Actor!.Character.Uid ||
                        !LegacyFloorItemVisibility.CanBroadcast(peer.Session.Character, entry) ||
                        !TryGet(peer.Connection, out var current) || !ReferenceEquals(current, peer.Session)) continue;
                    await SendDragonhowlDropFrameAsync(plan, state, ground, peer.Connection, frame, cancellationToken).ConfigureAwait(false);
                }
            }
            lock (owner.Frame.RequireActionLifecycleGate(this))
            {
                using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
                if (!DragonhowlDropPhaseHeldCurrent(plan, state) || ground is not null && !ground.IsCommittedCurrent()) return false;
                dragonhowlGroundAttempts.TryGetValue(attempt, out var step);
                step!.OutputCompleted = true;
            }
        }
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (state.Receipt is not null || !DragonhowlDropPhaseHeldCurrent(plan, state) || state.Next != state.Attempts.Count ||
                state.Attempts.Any(attempt => !dragonhowlGroundAttempts.TryGetValue(attempt, out var step) || !step.OutputCompleted)) return false;
            var receipt = new OwnedDragonhowlMonsterDropReceipt(); dragonhowlDropReceipts.Add(receipt, state); state.Receipt = receipt;
            return true;
        }
    }

    private async ValueTask SendDragonhowlDropFrameAsync(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        DragonhowlDropsState state, FloorItemRuntimeStateStore.PreparedDragonhowlGround ground,
        ConquerTcpConnection recipient, byte[] bytes, CancellationToken cancellationToken)
    {
        if (!IsDragonhowlDropOutputCurrent(plan, state, ground)) throw new InvalidOperationException("ownedDragonhowlDropOutputOwnerChanged");
        await recipient.SendAsync(bytes.ToArray(), cancellationToken).ConfigureAwait(false);
        if (!IsDragonhowlDropOutputCurrent(plan, state, ground)) throw new InvalidOperationException("ownedDragonhowlDropOutputOwnerChanged");
    }
    private bool IsDragonhowlDropOutputCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan, DragonhowlDropsState state,
        FloorItemRuntimeStateStore.PreparedDragonhowlGround ground)
    {
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            return DragonhowlDropPhaseHeldCurrent(plan, state) && ground.IsCommittedCurrent();
        }
    }
    private bool DragonhowlDropPhaseHeldCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan, DragonhowlDropsState state) =>
        state.Prepared && state.Started && !state.Disposed && state.Death.Active && DragonhowlDeathHeldCurrent(plan, state.Death) &&
        ReferenceEquals(state.Actor, state.Death.Source.Owner.Current[state.Death.Source.Owner.Context.Connection]) &&
        state.Recipe.MatchesActor(state.Actor!) && DragonhowlDropQualityCurrent(state) &&
        (state.Attempts.All(attempt => dragonhowlGroundAttempts.TryGetValue(attempt, out var row) && row.Data.SelectionAbsent) || DropOwnersCurrent(state));

    private bool HasCompletedDragonhowlDropPhase(WorldPacketHandlers.OwnedDragonhowlPreparation plan, DragonhowlMonsterDeathState death) =>
        dragonhowlDropsByDeath.TryGetValue(death, out var state) && state.Receipt is { } receipt &&
        dragonhowlDropReceipts.TryGetValue(receipt, out var issued) && ReferenceEquals(issued, state) &&
        DragonhowlDropPhaseHeldCurrent(plan, state) && state.Next == state.Attempts.Count;

    private bool HasPreparedDragonhowlDropPhase(DragonhowlMonsterDeathState death) =>
        dragonhowlDropsByDeath.TryGetValue(death, out var state) && state.Prepared && !state.Started && !state.Disposed &&
        state.Recipe.InputsAreCurrentPlain() && DragonhowlDropQualityCurrent(state) &&
        state.Attempts.All(attempt => dragonhowlGroundAttempts.TryGetValue(attempt, out var step) &&
            (step.Data.SelectionAbsent || DropOwnersCurrent(state) && step.Ground?.InputsAreCurrentPlain() == true));

    private void DisposeDragonhowlDrops(OwnedDragonhowlMonsterDrops token)
    {
        if (!dragonhowlDrops.TryGetValue(token, out var state)) return;
        lock (state.Death.Source.Owner.Frame.RequireActionLifecycleGate(this))
        {
            if (state.Disposed) return;
            state.Disposed = true;
            foreach (var attempt in state.Attempts)
                if (dragonhowlGroundAttempts.TryGetValue(attempt, out var step)) step.Ground?.Dispose();
        }
    }
}
