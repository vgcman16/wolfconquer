using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private enum DragonhowlSobEventStage { Prepared, DamageCommitted, ScoreCommitted, Published }

    private sealed partial class DragonhowlNonPlayerState
    {
        internal WorldPacketHandlers.PreparedDragonhowlSobEvent? SobEvent;
        internal DragonhowlSobEventStage SobEventStage;
        internal WorldSession? EventBefore, EventAfter;
        internal object? EventIdentity;
        internal bool EventObserverStarted;
        internal DragonhowlSobHeldCommit? SobEventHeld;
    }

    // Created only within the concrete event writer's retained sorted file
    // scope, before its score gate. It keeps subsequent target validation pure
    // while those same files and the actual actor gates remain owned.
    private sealed class DragonhowlSobHeldCommit : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly DragonhowlNonPlayerState data;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot native;
        private readonly long generation;
        internal DragonhowlSobHeldCommit(WorldSessionStore store, DragonhowlNonPlayerState data,
            LegacyOrdinarySpellBinding binding)
        {
            this.store = store; this.data = data; this.binding = binding;
            if (data.SobEventHeld is not null || !store.DragonhowlHeldCurrent(data.Owner) ||
                !data.Owner.Frame.TryReadBasicPhysicalNative(binding, out var captured, out generation) || captured is null)
                throw InvalidSpellPublication();
            native = captured;
            data.SobEventHeld = this;
        }
        internal bool IsCurrentPlain() => ReferenceEquals(data.SobEventHeld, this) &&
            store.DragonhowlHeldCurrent(data.Owner) && native.IsCurrentPlain(binding) &&
            data.Owner.Frame.IsBasicPhysicalNativeSnapshotCurrent(binding, native, generation);
        public void Dispose() { if (ReferenceEquals(data.SobEventHeld, this)) data.SobEventHeld = null; }
    }

    internal sealed partial class OwnedDragonhowlNonPlayer
    {
        internal bool CoversSobEventCommit(WorldPacketHandlers.PreparedDragonhowlSobEvent plan) =>
            store.dragonhowlNonPlayers.TryGetValue(this, out var data) && ReferenceEquals(data.SobEvent, plan) &&
            data.SobEventStage == DragonhowlSobEventStage.DamageCommitted &&
            data.Result is SobNpcDamageResult && data.Owner.Plan is { } actual &&
            data.Phase == data.Owner.NextPhase && ReferenceEquals(actual.Phases[data.Phase].NonPlayer, this) &&
            !data.Owner.Failed && data.SobEventHeld?.IsCurrentPlain() == true && data.CommittedTargetCurrent();

        internal bool CoversSobEventReset(SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget target,
            WorldPacketHandlers.PreparedDragonhowlSobEvent plan) =>
            store.dragonhowlNonPlayers.TryGetValue(this, out var data) && ReferenceEquals(data.SobTarget, target) &&
            ReferenceEquals(data.SobEvent, plan) && data.SobEventStage == DragonhowlSobEventStage.ScoreCommitted &&
            data.Result is SobNpcDamageResult { Killed: true } && data.Owner.Plan is { } actual &&
            data.Phase == data.Owner.NextPhase && ReferenceEquals(actual.Phases[data.Phase].NonPlayer, this) &&
            !data.Owner.Failed && data.SobEventHeld?.IsCurrentPlain() == true && plan.IsCurrentPlain();

        internal bool CanPrepareSobEventReset(WorldPacketHandlers.PreparedDragonhowlSobEvent plan) =>
            store.dragonhowlNonPlayers.TryGetValue(this, out var data) && data.Owner.Plan is null &&
            !data.Owner.Started && data.Result is null && plan.IsPreparedFor(this) &&
            store.DragonhowlHeldCurrent(data.Owner) && data.SobTarget is { } target &&
            target.CanPrepareEventReset(data.Damage, data.DeathBehavior, plan);
    }

    internal bool BindPreparedDragonhowlSobEvent(OwnedDragonhowlNonPlayer token,
        WorldPacketHandlers.PreparedDragonhowlSobEvent plan)
    {
        if (!dragonhowlNonPlayers.TryGetValue(token, out var data) || data.Owner.Plan is not null ||
            data.Owner.Started || data.SobEvent is not null || data.Sob is null || data.SobTarget is null ||
            !ReferenceEquals(data.CalculatedActor, plan.ActorBefore) || !plan.IsPreparedFor(token) ||
            !DragonhowlHeldCurrent(data.Owner) || !data.BeforeTargetCurrent() || !plan.IsCurrentPlain() ||
            (plan.ActorAfter.Character with { Attributes = plan.ActorBefore.Character.Attributes }) != plan.ActorBefore.Character ||
            !ReferenceEquals(plan.ActorAfter.PlayerItems, plan.ActorBefore.PlayerItems) ||
            !ReferenceEquals(plan.ActorAfter.PlayerSpells, plan.ActorBefore.PlayerSpells) ||
            !ReferenceEquals(plan.ActorAfter.PlayerProficiencies, plan.ActorBefore.PlayerProficiencies)) return false;
        data.SobEvent = plan;
        return true;
    }

    private bool TryCommitDragonhowlSobEventHeld(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, DragonhowlNonPlayerState data)
    {
        if (data.SobEvent is not { } change || data.SobTarget is not { } target ||
            !change.IsPreparedFor(token) || !change.IsCurrentPlain() || data.SobEventStage != DragonhowlSobEventStage.Prepared)
            return false;
        var owner = data.Owner;
        var binding = owner.Bindings[owner.Context.Connection];
        var before = owner.Current[binding.Connection];
        var proposed = before with { Character = before.Character with { Attributes = change.ActorAfter.Character.Attributes } };
        if (!_sessions.TryGetValue(binding.Connection, out var holder) || !ReferenceEquals(holder.Read(), before)) return false;
        var paths = new[] { Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            owner.Frame.RequireDragonhowlGearPath(owner.Request) };
        return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
        {
            if (!TryReadDragonhowlNonPlayerBefore(token, out var actual) || !ReferenceEquals(actual, data) ||
                !plan.InputsAreCurrentPlain() || !change.IsCurrentPlain()) return false;
            using var publication = new PreparedSessionSpellPublication(this, binding.Connection, holder, before, proposed);
            // These concrete events change their named runtime score fields.
            // They cannot borrow this stage for an unrelated persisted scalar.
            if (publication.HitpointsWrite is not null) return false;
            publication.ArmFinalProof();
            if (!publication.ConsumeBeforeWriterPlain()) return false;
            using (var held = new DragonhowlSobHeldCommit(this, data, binding))
            using (change.EnterCommitScope())
            {
                if (!held.IsCurrentPlain() || !change.IsCurrentPlain() || !data.BeforeTargetCurrent()) return false;
                owner.Committed = true;
                if (!target.TryCommit(token, out var result) || result.AlreadyDead ||
                    result.DamageApplied != Math.Min(data.Damage, target.Hitpoints)) return false;
                data.Result = result;
                data.SobEventStage = DragonhowlSobEventStage.DamageCommitted;
                if (!change.PublishScorePrepared()) return false;
                data.SobEventStage = DragonhowlSobEventStage.ScoreCommitted;
                if (change.ExpectedResetSob is not null && !target.TryCommitEventReset(token, change)) return false;
                if (!publication.IsCurrentPlain() || !change.IsCurrentPlain() || !data.CommittedTargetCurrent())
                    throw new InvalidOperationException("ownedDragonhowlSobEventPublicationChanged");
                publication.PublishNative();
                publication.PublishHolder();
                data.EventBefore = before; data.EventAfter = publication.FinalSession;
                data.EventIdentity = publication.PublishedIdentity;
                owner.Current[binding.Connection] = publication.FinalSession;
                data.SobEventStage = DragonhowlSobEventStage.Published;
            }
            PersistFrameOwnedSessionChange(binding.Connection, before, publication.FinalSession,
                publication.PublishedIdentity, binding);
            return true;
        });
    }

    internal bool TryReadDragonhowlSobEventPublication(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, WorldPacketHandlers.PreparedDragonhowlSobEvent change,
        out WorldSession actor, out SobNpc original, out SobNpc after, out SobNpcDamageResult result)
    {
        actor = null!; original = null!; after = null!; result = default;
        return dragonhowlNonPlayers.TryGetValue(token, out var data) && ReferenceEquals(data.SobEvent, change) &&
            data.SobEventStage == DragonhowlSobEventStage.Published && data.EventAfter is not null &&
            TryReadDragonhowlSobPublication(plan, token, out actor, out original, out after, out result) &&
            ReferenceEquals(actor, data.EventAfter) && change.IsCurrentPlain();
    }

    internal async ValueTask<bool> PublishDragonhowlSobEventAsync(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlNonPlayer token, CancellationToken cancellationToken)
    {
        if (!dragonhowlNonPlayers.TryGetValue(token, out var data) || data.SobEvent is not { } change ||
            data.EventBefore is not { } before || data.EventAfter is not { } after || data.EventIdentity is not { } identity)
            return false;
        var owner = data.Owner;
        var binding = owner.Bindings[owner.Context.Connection];
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (data.EventObserverStarted || !DragonhowlHeldCurrent(owner) || !ReferenceEquals(owner.Current[binding.Connection], after) ||
                data.SobEventStage != DragonhowlSobEventStage.Published || !data.CommittedTargetCurrent() || !change.IsCurrentPlain()) return false;
            data.EventObserverStarted = true;
        }
        // The exact successor and stage are retained before a real observer
        // can throw. A caller cannot retry the committed damage or score.
        NotifyPublishedSessionChange(binding.Connection, before, after, identity, binding);
        return await change.PublishAsync(plan, token, cancellationToken).ConfigureAwait(false);
    }
}
