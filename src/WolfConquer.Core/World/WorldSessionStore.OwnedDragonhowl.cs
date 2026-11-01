using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlOwner { }
    private sealed class DragonhowlOwnerState(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
        MsgAttackPacket packet, WorldServerFrameHandler.OwnedDragonhowlPrimary primary, WorldActiveSession? primaryPlayer,
        WorldServerFrameHandler.OwnedDragonhowlRequest request,
        WorldActiveSession[] participants)
    {
        internal readonly WorldServerFrameHandler Frame = frame;
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly MsgAttackPacket Packet = packet;
        internal readonly WorldServerFrameHandler.OwnedDragonhowlPrimary Primary = primary;
        internal readonly WorldActiveSession? PrimaryPlayer = primaryPlayer;
        internal readonly WorldServerFrameHandler.OwnedDragonhowlRequest Request = request;
        internal readonly Dictionary<ConquerTcpConnection, WorldSession> Current = participants.ToDictionary(
            row => row.Connection, row => row.Session);
        internal readonly Dictionary<ConquerTcpConnection, LegacyOrdinarySpellBinding> Bindings = participants.ToDictionary(
            row => row.Connection, row => row.Session.NativeGameClient!.OrdinarySpellBinding!);
        internal WorldPacketHandlers.OwnedDragonhowlPreparation? Plan;
        internal WorldServerFrameHandler.OwnedDragonhowlAttempt? Attempt;
        internal readonly Dictionary<int, PreparedDragonhowlDeath> Deaths = new();
        internal readonly Dictionary<ConquerTcpConnection, DragonhowlExperienceAwardState> CompletedExperience = new();
        internal readonly Dictionary<ConquerTcpConnection, DragonhowlExperienceAwardState> CompletedAwards = new();
        internal readonly Dictionary<ConquerTcpConnection, DragonhowlExperienceAwardState> PlannedAwards = new();
        internal readonly Dictionary<ConquerTcpConnection, DragonhowlScalarQueryOrigin> ScalarQueries = new();
        internal int NextPhase;
        internal string CommitStage = "notStarted";
        internal bool Started, Committed, Completed, Failed;
    }
    private readonly ConditionalWeakTable<OwnedDragonhowlOwner, DragonhowlOwnerState> dragonhowlOwners = new();

    internal bool TryPrepareOwnedDragonhowl(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession primary, out WorldPacketHandlers.OwnedDragonhowlPreparation? prepared, out string reason)
        => TryPrepareOwnedDragonhowlCore(context, packet, new DragonhowlInitialTarget(primary), out prepared, out reason);

    private bool TryPrepareOwnedDragonhowlCore(WorldPacketHandlerContext context, MsgAttackPacket packet,
        DragonhowlInitialTarget initialTarget, out WorldPacketHandlers.OwnedDragonhowlPreparation? prepared, out string reason)
    {
        prepared = null; reason = "ownedDragonhowlOwnerUnavailable";
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } actor ||
            !frame.TryPrepareActionContext(this, context, actor, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            // The real exclusive Frame operation serializes gameplay; lifecycle
            // also excludes worker publications while this first capture runs.
            WorldServerFrameHandler.OwnedDragonhowlRequest? request;
            WorldServerFrameHandler.OwnedDragonhowlPrimary? primary;
            WorldPacketHandlers.LegacyPhysicalCombatGearSelection? selection;
            var initial = initialTarget.Player is { } player
                ? new[] { new WorldActiveSession(actual.Connection, actor), player }
                : new[] { new WorldActiveSession(actual.Connection, actor) };
            using (var selectionScope = new DragonhowlOwnerScope(this, initial))
                if (!selectionScope.IsCurrentPlain() || !initialTarget.TryCapture(frame, actual, packet, out primary) || primary is null ||
                    !frame.TryPrepareOwnedDragonhowlRequest(actual, packet, primary, out request) ||
                    request is null || !frame.TryReadOwnedDragonhowl(request, out selection) || selection is null) return false;
            if (!WorldPacketHandlers.TrySelectDragonhowlPlayerParticipants(actual, packet, initialTarget.Player, selection,
                    Snapshot(), out var participants) || participants.Any(row =>
                    row.Session.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false })) return false;
            using var held = new DragonhowlOwnerScope(this, participants);
            if (!held.IsCurrentPlain() || !frame.TryReadOwnedDragonhowl(request, out var capturedSelection) ||
                !ReferenceEquals(capturedSelection, selection)) return false;
            var owner = new OwnedDragonhowlOwner();
            var state = new DragonhowlOwnerState(frame, actual, packet, primary, initialTarget.Player, request, participants);
            dragonhowlOwners.Add(owner, state);
            WorldPacketHandlers.OwnedDragonhowlPreparation? plan = null;
            try
            {
                using var permission = TeamSessionStateStore.OwnedDragonhowlPermission.Prepare(
                    actual.TeamStates ?? throw InvalidSpellPublication(), this, owner);
                if (!WorldPacketHandlers.TryPrepareOwnedDragonhowlActual(this, owner, actual, packet, initialTarget.Player,
                        selection, participants, out plan, out reason) || plan is null)
                { if (string.IsNullOrEmpty(reason)) reason = "ownedDragonhowlPlanUnavailable"; return false; }
                if (!frame.OwnedDragonhowlSavedGearCurrent(request))
                { reason = "ownedDragonhowlSavedGearChanged"; return false; }
                if (!held.IsCurrentPlain())
                { reason = "ownedDragonhowlParticipantsChanged"; return false; }
                if (!permission.IsCurrentPlain())
                { reason = "ownedDragonhowlPermissionChanged"; return false; }
                // These are genuine writer/native preparations. They do not
                // publish, consume a request, or serve as a successful dry run.
                for (int phaseIndex = 0; phaseIndex < plan.Phases.Count; phaseIndex++)
                {
                    var phase = plan.Phases[phaseIndex];
                    if (phase.FutureAward is { } futureAward)
                    {
                        if (!futureAward.IsPreparedPhase(phase)) return false;
                        continue;
                    }
                    if (phase.FutureDeath is { } futureDeath)
                    {
                        // Exact future recipe/images are DATA here. A live
                        // death writer cannot exist until this proc's actual
                        // completed-XP successor reaches this phase.
                        if (!futureDeath.IsPreparedPhase(phase)) return false;
                        continue;
                    }
                    if (phase.NonPlayer is not null)
                    {
                        if (!phase.NonPlayer.InputsAreCurrentPlain()) return false;
                        if (phase.MonsterRewards is { } rewards &&
                            (!rewards.IsPreparedCurrent() || phase.TeamExperience is not { } team ||
                             !IsPreparedDragonhowlTeamExperienceCurrent(team))) return false;
                        if (phase.SobEvent is null) continue;
                        if (!phase.SobEvent.IsPreparedFor(phase.NonPlayer) || !phase.SobEvent.IsCurrentPlain()) return false;
                    }
                    var original = state.Current[phase.Connection];
                    if (!_sessions.TryGetValue(phase.Connection, out var holder) || !ReferenceEquals(holder.Read(), original)) return false;
                    var death = phase.Death;
                    var writer = death is null ? null : LegacyPreparedBasicPhysicalWrite.PrepareDragonhowl(frame, this, death);
                    using var publication = new PreparedSessionSpellPublication(this, phase.Connection,
                        holder, original, phase.After, physicalDeath: death?.Death,
                        physicalDeathPublication: writer?.PreparePublication(), dragonhowlDeath: death);
                    using var settlement = death is null ? null : new PreparedDragonhowlDeath(this, state, death, writer!, publication);
                    publication.ArmFinalProof();
                    if (!publication.ConsumeBeforeWriterPlain())
                    { reason = "ownedDragonhowlBeforeWriterUnavailable:" + phaseIndex; return false; }
                    if (settlement is not null && !settlement.IsCurrentPlain())
                    { reason = "ownedDragonhowlDeathSettlementUnavailable:" + phaseIndex; return false; }
                }
                // Unaffected line spectators supplied permission observations,
                // not mutation/retirement ownership. Keep only real phases and
                // the selected primary used by the actual request/repeat owner.
                permission.Dispose();
                foreach (var connection in state.Current.Keys.ToArray())
                    if (!ReferenceEquals(connection, actual.Connection) && !ReferenceEquals(connection, initialTarget.Player?.Connection) &&
                        !plan.TryGetFinalProjection(connection, out _) && !plan.UsesRewardParticipant(connection))
                    { state.Current.Remove(connection); state.Bindings.Remove(connection); }
                state.Plan = plan;
                prepared = plan; reason = string.Empty;
                return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                IOException or UnauthorizedAccessException or OverflowException or FormatException)
            {
                var trace = string.Join(" | ", (error.StackTrace ?? string.Empty).Split('\n').Take(3)).Replace("\r", string.Empty);
                reason = "ownedDragonhowlPreflight:" + error.GetType().Name + ":" + error.Message + " [" + trace + "]";
                return false;
            }
            finally { if (prepared is null) plan?.DisposePreparedRewards(); }
        }
    }

    internal bool IsOwnedDragonhowlBeforeCurrent(OwnedDragonhowlOwner owner) =>
        dragonhowlOwners.TryGetValue(owner, out var state) && !state.Started && !state.Failed &&
        DragonhowlHeldCurrent(state) && state.Frame.TryReadOwnedDragonhowl(state.Request, out _);

    internal bool IsDragonhowlReceiveCompleted(OwnedDragonhowlOwner owner, ConquerTcpConnection target) =>
        dragonhowlOwners.TryGetValue(owner, out var state) && state.Plan is { } plan && !state.Failed && state.Committed &&
        DragonhowlHeldCurrent(state) && plan.Phases.Select((phase, index) => (phase, index))
            .Where(row => ReferenceEquals(row.phase.Connection, target)).All(row => row.index < state.NextPhase);

    internal bool TryReadDragonhowlPermission(OwnedDragonhowlOwner owner, TeamSessionStateStore teams,
        out LegacyOrdinarySpellBinding[] bindings)
    {
        bindings = [];
        if (!dragonhowlOwners.TryGetValue(owner, out var state) || state.Failed ||
            !ReferenceEquals(state.Context.TeamStates, teams) || !DragonhowlHeldCurrent(state)) return false;
        bindings = state.Bindings.Values.ToArray();
        return true;
    }

    private bool DragonhowlHeldCurrent(DragonhowlOwnerState state)
    {
        if (!Monitor.IsEntered(state.Frame.RequireActionLifecycleGate(this)) ||
            !state.Frame.IsActionLifecycleCurrent(this, state.Context) ||
            state.Context.TradeOperations?.OwnsActiveExclusiveLease(state.Context.FrameOperationLease) != true) return false;
        foreach (var pair in state.Current)
        {
            var binding = state.Bindings[pair.Key];
            if (!_connectionGates.TryGetValue(pair.Key, out var gate) || !Monitor.IsEntered(gate) ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !_sessions.TryGetValue(pair.Key, out var holder) || !ReferenceEquals(holder.Read(), pair.Value) ||
                !ReferenceEquals(holder.SpellBinding, binding) || !BindingIsCurrentPlain(binding, holder, pair.Value)) return false;
        }
        return true;
    }

    private sealed class DragonhowlOwnerScope : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly WorldActiveSession[] rows;
        private readonly Stack<IDisposable> scopes = new();
        private readonly Stack<object> gates = new();
        private IDisposable? package;
        internal DragonhowlOwnerScope(WorldSessionStore store, WorldActiveSession[] rows)
        {
            this.store = store; this.rows = rows;
            try
            {
                package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                    rows.Select(row => row.Session).ToArray());
                var ordered = rows.OrderBy(row => GetConnectionGateOrder(row.Connection)).ToArray();
                foreach (var row in ordered)
                {
                    if (!store._connectionGates.TryGetValue(row.Connection, out var gate)) throw InvalidSpellPublication();
                    Monitor.Enter(gate); gates.Push(gate);
                }
                scopes.Push(EnterNativeConnectionGate());
                foreach (var row in ordered)
                    scopes.Push(new SpellActorScope(row.Session.NativeGameClient?.OrdinarySpellBinding ?? throw InvalidSpellPublication()));
                foreach (var row in ordered)
                    scopes.Push(new DragonhowlMonitorScope(row.Session.NativeGameClient!.OrdinarySpellBinding!.Player.BitVector.SyncRoot));
            }
            catch { Dispose(); throw; }
        }
        internal bool IsCurrentPlain() => rows.All(row => store._sessions.TryGetValue(row.Connection, out var holder) &&
            ReferenceEquals(holder.Read(), row.Session) && holder.SpellBinding is { } binding &&
            store.BindingIsCurrentPlain(binding, holder, row.Session));
        public void Dispose()
        {
            while (scopes.TryPop(out var scope)) scope.Dispose();
            while (gates.TryPop(out var gate)) Monitor.Exit(gate);
            package?.Dispose(); package = null;
        }
    }

    private sealed class DragonhowlMonitorScope : IDisposable
    {
        private readonly object gate;
        private bool held;
        internal DragonhowlMonitorScope(object gate) { this.gate = gate; Monitor.Enter(gate, ref held); }
        public void Dispose() { if (held) { held = false; Monitor.Exit(gate); } }
    }
}
