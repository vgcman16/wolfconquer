using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlCompletion { }
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedDragonhowlCompletion, DragonhowlOwnerState>
        dragonhowlCompletions = new();

    internal bool IsActualDragonhowlCompletion(OwnedDragonhowlCompletion completion,
        LegacyOrdinarySpellBinding binding, WorldServerFrameHandler.BasicPhysicalAttempt attempt) =>
        dragonhowlCompletions.TryGetValue(completion, out var actual) && actual.Attempt is { } proc &&
        actual.Frame.IsDragonhowlBasicAttempt(proc, attempt) && IsActualDragonhowlCompletion(completion, binding, proc);

    internal bool IsActualDragonhowlCompletion(OwnedDragonhowlCompletion completion,
        LegacyOrdinarySpellBinding binding, WorldServerFrameHandler.OwnedDragonhowlAttempt attempt) =>
        dragonhowlCompletions.TryGetValue(completion, out var state) && state.Plan is { } plan &&
        !state.Failed && state.Committed && state.NextPhase == plan.Phases.Count && plan.HasCompletedActualOutput &&
        ReferenceEquals(state.Attempt, attempt) && ReferenceEquals(state.Bindings[state.Context.Connection], binding) &&
        DragonhowlHeldCurrent(state);

    internal bool CompleteDragonhowl(WorldPacketHandlers.OwnedDragonhowlPreparation plan)
    {
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var state) || !ReferenceEquals(state.Plan, plan) ||
            state.Failed || state.Completed || !state.Committed || state.NextPhase != plan.Phases.Count || !plan.HasCompletedActualOutput) return false;
        LegacyOrdinarySpellBinding? armedActor = null;
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? armed = null;
        lock (state.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(state));
            if (!DragonhowlHeldCurrent(state) || !plan.InputsAreCurrentPlain() || state.Attempt is not { } attempt ||
                !state.Frame.IsDragonhowlActive(state.Request, attempt, state.Current[state.Context.Connection],
                    DragonhowlCurrentPrimary(state))) return false;
            var completion = new OwnedDragonhowlCompletion();
            dragonhowlCompletions.Add(completion, state);
            var binding = state.Bindings[state.Context.Connection];
            var actor = state.Current[binding.Connection];
            var target = DragonhowlCurrentPrimary(state);
            if (!state.Frame.TryReadDragonhowlPrimary(state.Primary, target, out _, out var x, out var y, out var alive)) return false;
            bool sourceArmsRepeat = actor.Character.MinHitPoints > 0 && alive && (target is null ||
                Math.Max(Math.Abs(actor.Character.X - x), Math.Abs(actor.Character.Y - y)) <= 3);
            if (sourceArmsRepeat)
            {
                // A source no-repeat outcome is distinct from losing the real
                // completion/native owner after this cast has committed.
                if (!state.Frame.TryPrepareDragonhowlRepeat(state.Request, attempt, completion, actor, target,
                        out var before, out var generation, out var query) || before is null) return false;
                var change = IssueBasicPhysicalChange(state.Frame, binding, state.Current[binding.Connection], before,
                    generation, LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.ArmRepeat, query, null, state.Context);
                var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(binding, change);
                var runtime = state.Frame.PrepareDragonhowlNativePublication(this, binding, change, native, attempt);
                if (!native.IsCurrentPlain() || !runtime.IsCurrentPlain()) return false;
                native.PublishPrepared(); runtime.PublishPrepared();
                armedActor = binding; armed = native.After;
            }
            state.Completed = true;
        }
        if (armedActor is not null && armed is not null) state.Frame.NotifyActualBasicPhysicalArm(this, armedActor, armed);
        return true;
    }

    internal void EndDragonhowl(WorldPacketHandlers.OwnedDragonhowlPreparation plan)
    {
        if (dragonhowlOwners.TryGetValue(plan.Owner, out var state) && ReferenceEquals(state.Plan, plan) &&
            state.Attempt is { } attempt) state.Frame.EndDragonhowlAttempt(attempt);
    }

    internal bool TryCommitDragonhowlPhase(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
        out WorldSession? published)
    {
        published = null;
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var state) || !ReferenceEquals(state.Plan, plan) ||
            state.Failed || state.Completed || state.NextPhase != index || index < 0 || index >= plan.Phases.Count) return false;
        var phase = plan.Phases[index];
        WorldSession before; WorldSession after; LegacyOrdinarySpellBinding binding;
        lock (state.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(state));
            state.CommitStage = "ownerInputs";
            if (!DragonhowlHeldCurrent(state) || !plan.InputsAreCurrentPlain()) return false;
            using var permission = TeamSessionStateStore.OwnedDragonhowlPermission.Prepare(
                state.Context.TeamStates ?? throw InvalidSpellPublication(), this, plan.Owner);
            if (!state.Started)
            {
                state.CommitStage = "beginAttempt";
                if (index != 0 || !state.Frame.OwnedDragonhowlSavedGearCurrent(state.Request) ||
                    !state.Frame.TryBeginDragonhowlAttempt(state.Request, out var attempt) || attempt is null) return false;
                state.Attempt = attempt; state.Started = true;
            }
            var actor = state.Bindings[state.Context.Connection];
            state.CommitStage = "activeAttempt";
            if (state.Attempt is not { } actualAttempt || !state.Frame.IsDragonhowlActive(state.Request, actualAttempt,
                    state.Current[actor.Connection], DragonhowlCurrentPrimary(state))) return false;
            if (phase.NonPlayer is { } nonPlayer)
            {
                if (!TryCommitDragonhowlNonPlayerHeld(plan, index, nonPlayer)) return false;
                state.NextPhase++;
                published = state.Current[state.Context.Connection];
                return true; // The concrete target/event receipt owns its output.
            }
            binding = state.Bindings[phase.Connection]; before = state.Current[phase.Connection];
            if (phase.Death is { } purpose && !BindDragonhowlDeath(plan, index, purpose)) return false;
            // Only the concrete phase's owned fields change. The actual prior
            // canonical publication supplies all other current owners.
            var candidate = phase.Death is not null ? phase.After : before with { Character = phase.After.Character,
                PhysicalStatusOverlay = phase.After.PhysicalStatusOverlay, MagicCooldowns = phase.After.MagicCooldowns,
                CanAttackFlashingRegistration = phase.After.CanAttackFlashingRegistration };
            if (!_sessions.TryGetValue(phase.Connection, out var holder) || !ReferenceEquals(holder.Read(), before)) return false;
            var deathWriter = phase.Death is null ? null : LegacyPreparedBasicPhysicalWrite.PrepareDragonhowl(state.Frame, this, phase.Death);
            var paths = state.Bindings.Values.Select(row => Path.Combine(row.UsersRoot,
                row.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"))
                .Append(state.Frame.RequireDragonhowlGearPath(state.Request))
                .Concat(deathWriter is null ? [] : new[] { deathWriter.CharacterPath, deathWriter.ItemPath, deathWriter.SpellPath }).ToArray();
            WorldSession? committed = null;
            bool accepted = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                state.CommitStage = "lockedInputs";
                if (!DragonhowlHeldCurrent(state) || !permission.IsCurrentPlain() || !plan.InputsAreCurrentPlain() ||
                    !state.Frame.IsDragonhowlActive(state.Request, actualAttempt,
                        state.Current[actor.Connection], DragonhowlCurrentPrimary(state))) return false;
                using var publication = new PreparedSessionSpellPublication(this, phase.Connection, holder, before, candidate,
                    physicalDeath: phase.Death?.Death, physicalDeathPublication: deathWriter?.PreparePublication(), dragonhowlDeath: phase.Death);
                using var death = phase.Death is null ? null : new PreparedDragonhowlDeath(this, state, phase.Death, deathWriter!, publication);
                var flashing = phase.Flashing?.Native.PreparePublication(plan, index, before, candidate);
                LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication? native = null;
                WorldServerFrameHandler.BasicPhysicalNativePublication? runtime = null;
                if (index == 0)
                {
                    state.CommitStage = "nativeStamp";
                    if (!state.Frame.TryReadDragonhowlStamp(state.Request, actualAttempt, out var captured,
                            out var generation, out var stamp) || captured is null) return false;
                    var change = IssueBasicPhysicalChange(state.Frame, actor, before, captured, generation,
                        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit, null, stamp, state.Context);
                    native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(actor, change);
                    runtime = state.Frame.PrepareDragonhowlNativePublication(this, actor, change, native, actualAttempt);
                }
                publication.ArmFinalProof();
                state.CommitStage = "beforeWriter";
                if (!publication.ConsumeBeforeWriterPlain() || !DragonhowlHeldCurrent(state) ||
                    death is not null && !death.ConsumeBeforeWriterPlain() ||
                    flashing is not null && !flashing.IsCurrentPlain() ||
                    native is not null && (!native.IsCurrentPlain() || runtime?.IsCurrentPlain() != true)) return false;
                if (publication.HitpointsWrite is { } writer)
                {
                    if (deathWriter is null)
                    {
                        state.CommitStage = "hitpointsWriter";
                        var result = writer.TryCommit(out var reason);
                        if (result == LegacyBoundHitpointsScalarWrite.Outcome.RecoveryRequired)
                        { state.Committed = true; throw new InvalidOperationException("ownedDragonhowlPersistenceUnproven:" + reason); }
                        if (result != LegacyBoundHitpointsScalarWrite.Outcome.Committed) return false;
                    }
                    else
                    {
                        var result = deathWriter.TryCommit(state.Context.PhysicalDeathJournalInterruption, out var reason);
                        if (result == LegacyPreparedBasicPhysicalWrite.Outcome.RecoveryRequired)
                        { state.Committed = true; throw new InvalidOperationException("ownedDragonhowlDeathPersistenceUnproven:" + reason); }
                        if (result != LegacyPreparedBasicPhysicalWrite.Outcome.Committed) return false;
                    }
                    state.Committed = true;
                    if (!publication.AcceptExternallyPersistedHitpoints())
                        throw new InvalidOperationException("ownedDragonhowlCommittedImageChanged");
                }
                state.CommitStage = "afterWriter";
                if (!publication.IsCurrentPlain() || !DragonhowlHeldCurrent(state) || !permission.IsCurrentPlain() ||
                    death is not null && !death.IsCommittedWriterCurrentPlain() ||
                    flashing is not null && !flashing.IsCurrentPlain() ||
                    native is not null && (!native.IsCurrentPlain() || runtime?.IsCurrentPlain() != true))
                    throw new InvalidOperationException("ownedDragonhowlPublicationChanged");
                // From this point plain prepared live participants may have
                // changed even if an unexpected publication exception occurs.
                state.Committed = true;
                publication.PublishNative(); death?.PublishPrepared(); flashing?.PublishPrepared(); native?.PublishPrepared(); runtime?.PublishPrepared();
                publication.PublishHolder();
                state.Committed = true;
                committed = publication.FinalSession;
                // Retain the real successor before the observer can throw.
                state.Current[phase.Connection] = committed;
                if (death is not null) state.Deaths.Add(index, death);
                state.NextPhase++;
                death?.Dispose();
                publication.Dispose();
                PersistFrameOwnedSessionChange(phase.Connection, before, committed, binding.NativeIdentity,
                    binding, publication.HitpointsWrite, completedDragonhowlDeath: death?.Receipt);
                return true;
            });
            if (!accepted || committed is null) return false;
            after = committed;
        }
        published = after;
        NotifyPublishedSessionChange(phase.Connection, before, after, binding.NativeIdentity, binding);
        return true;
    }

    internal bool TryReadDragonhowlCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        ConquerTcpConnection connection, out WorldSession current)
    {
        current = null!;
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var state) || !ReferenceEquals(state.Plan, plan) || state.Failed) return false;
        lock (state.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(state));
            if (!DragonhowlHeldCurrent(state) || !plan.InputsAreCurrentPlain() || state.Attempt is not { } attempt ||
                !state.Frame.IsDragonhowlActive(state.Request, attempt, state.Current[state.Context.Connection],
                    DragonhowlCurrentPrimary(state)) || !state.Current.TryGetValue(connection, out current!)) return false;
            return true;
        }
    }

    // Diagnostic only; never consulted by any phase/admission authority.
    internal string DescribeDragonhowlPhaseFailure(WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index) =>
        dragonhowlOwners.TryGetValue(plan.Owner, out var state) && ReferenceEquals(state.Plan, plan)
            ? $"phase={index};stage={state.CommitStage};next={state.NextPhase};started={state.Started};committed={state.Committed}"
            : $"phase={index};stage=ownerUnavailable";

    private static WorldActiveSession[] DragonhowlCurrentRows(DragonhowlOwnerState state) =>
        state.Current.Select(pair => new WorldActiveSession(pair.Key, pair.Value)).ToArray();
}
