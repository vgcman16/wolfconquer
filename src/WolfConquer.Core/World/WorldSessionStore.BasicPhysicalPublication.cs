using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class BasicPhysicalPublication
    {
        private readonly WorldSessionStore store;
        // Inert until the actual Store's private table registers this token.
        internal BasicPhysicalPublication(WorldSessionStore store) => this.store = store;
        internal string OutputPhase => store.basicPhysicalPublications.TryGetValue(this, out var actual)
            ? actual.OutputPhase : "unregistered";

        internal bool IsPreparedDeathPublication(WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation,
            WorldSession canonicalActor, WorldSession canonicalTarget) =>
            store.basicPhysicalPublications.TryGetValue(this, out var actual) && actual.CommittedWriter is null &&
            ReferenceEquals(actual.Plan.Death, preparation) && ReferenceEquals(actual.Actor, canonicalActor) &&
            ReferenceEquals(actual.Target, canonicalTarget) && actual.Plan.IsCurrentPlain() &&
            (actual.ProficiencyWrite.Plan.HasChanges ? actual.Plan.CanonicalProgressionOutcomesMatch(canonicalActor, canonicalTarget, actual.ProficiencyWrite.Plan) :
                actual.Plan.CanonicalOutcomesMatch(canonicalActor, canonicalTarget)) && preparation.IsCurrentPlain(actual.TargetBinding);

        internal bool TryReadCommittedDeathPublication(WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation,
            WorldSession canonicalActor, WorldSession canonicalTarget, out WorldPacketHandlerContext context)
        {
            context = null!;
            if (!store.basicPhysicalPublications.TryGetValue(this, out var actual) ||
                !actual.DeathOutputStarted || actual.BaseOutputCompleted ||
                !ReferenceEquals(actual.Plan.Death, preparation) || !ReferenceEquals(actual.Actor, canonicalActor) ||
                !ReferenceEquals(actual.Target, canonicalTarget) || !store.IsBasicPhysicalPublicationCurrent(actual)) return false;
            context = actual.Context with { Session = actual.Actor };
            return true;
        }
    }

    private sealed class BasicPhysicalPublicationData
    {
        internal readonly WorldServerFrameHandler Frame;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation Plan;
        internal readonly LegacyOrdinarySpellBinding ActorBinding, TargetBinding;
        internal WorldSession Actor;
        internal readonly WorldSession Target;
        internal WorldSession? ActorBeforeProficiency;
        internal readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot NativeActor;
        internal readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? NativeDeadTarget;
        internal readonly LegacyNativeMagicQueryClient.OwnedPhysicalDeathPublication? DeathNative;
        internal readonly LegacyNativeStatusFlagsBigVector32.OwnedPhysicalDeathWindWalkerPublication? DeathWindWalker;
        internal readonly LegacyNativeAttributeMapProof ActorAttributes, TargetAttributes, ActorPaid, TargetPaid;
        internal readonly LegacyNativeActorScalarState.NativeValueProof ActorScalars, TargetScalars;
        internal readonly LegacyActionState.NativeValueProof ActorAction, TargetAction;
        internal readonly ushort ActorStamina, TargetStamina;
        internal readonly LegacyPreparedBasicPhysicalProficiencyWrite ProficiencyWrite;
        internal readonly LegacyPreparedBasicPhysicalWrite? DeathFileWriter;
        internal LegacyNativeSpellStorage.PreparedSessionSpellMap ActorSpellGraph;
        internal LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap ActorProficiencyGraph;
        internal readonly LegacyNativeSpellStorage.PreparedSessionSpellMap TargetSpellGraph;
        internal readonly LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap TargetProficiencyGraph;
        internal LegacyPreparedBasicPhysicalHitProgressionWrite? JointProgressionWriter;
        internal LegacyBoundHitpointsScalarWrite? CommittedWriter;
        internal WorldPacketHandlers.OwnedPhysicalDeathNotifications? DeathNotifications;
        internal bool OutputStarted, DeathOutputStarted, BaseOutputCompleted, ProficiencyCompleted, CompletedOutput;
        internal string OutputPhase = "prepared";

        internal BasicPhysicalPublicationData(WorldServerFrameHandler frame,
            WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation plan, WorldSession actor, WorldSession target,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot nativeActor,
            LegacyNativeMagicQueryClient.OwnedPhysicalDeathPublication? deathNative,
            LegacyNativeStatusFlagsBigVector32.OwnedPhysicalDeathWindWalkerPublication? deathWindWalker,
            LegacyPreparedBasicPhysicalProficiencyWrite proficiencyWrite,
            LegacyNativeSpellStorage.PreparedSessionSpellMap actorSpellGraph,
            LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap actorProficiencyGraph,
            LegacyNativeSpellStorage.PreparedSessionSpellMap targetSpellGraph,
            LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap targetProficiencyGraph,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? nativeDeadTarget,
            LegacyPreparedBasicPhysicalWrite? deathFileWriter)
        {
            Frame = frame; Plan = plan; Context = plan.Context;
            ActorBinding = plan.ActorBinding; TargetBinding = plan.TargetBinding;
            Actor = actor; Target = target; NativeActor = nativeActor; DeathNative = deathNative;
            NativeDeadTarget = nativeDeadTarget;
            DeathWindWalker = deathWindWalker;
            ProficiencyWrite = proficiencyWrite;
            DeathFileWriter = deathFileWriter;
            ActorSpellGraph = actorSpellGraph; ActorProficiencyGraph = actorProficiencyGraph;
            TargetSpellGraph = targetSpellGraph; TargetProficiencyGraph = targetProficiencyGraph;
            if (!proficiencyWrite.IsPreparedFor(plan)) throw InvalidSpellPublication();
            ActorAttributes = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.Attributes);
            TargetAttributes = LegacyNativeAttributeMapProof.CaptureRaw(target.Character.Attributes);
            ActorPaid = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.PaidNobility);
            TargetPaid = LegacyNativeAttributeMapProof.CaptureRaw(target.Character.PaidNobility);
            ActorScalars = new(actor.Character); TargetScalars = new(target.Character);
            ActorAction = new(actor.Character); TargetAction = new(target.Character);
            ActorStamina = LegacyStaminaState.RequireCurrent(actor.Character);
            TargetStamina = LegacyStaminaState.RequireCurrent(target.Character);
        }

        internal bool FinalInputsCurrentPlain() => ActorAttributes.IsCurrentPlain() && TargetAttributes.IsCurrentPlain() &&
            ActorPaid.IsCurrentPlain() && TargetPaid.IsCurrentPlain() && ActorScalars.IsCurrentPlain() && TargetScalars.IsCurrentPlain() &&
            ActorAction.IsCurrentPlain() && TargetAction.IsCurrentPlain() && NativeActor.IsCurrentPlain(ActorBinding) &&
            (NativeDeadTarget is null || NativeDeadTarget.IsCurrentPlain(TargetBinding)) &&
            ActorSpellGraph.HasCurrentCommittedGraph() && ActorProficiencyGraph.HasCurrentCommittedGraph() &&
            TargetSpellGraph.HasCurrentCommittedGraph() && TargetProficiencyGraph.HasCurrentCommittedGraph() &&
            ActorBinding.Items.IsCurrentPlain() && TargetBinding.Items.IsCurrentPlain() &&
            (DeathNative is null || DeathNative.IsPublishedCurrentPlain()) &&
            (DeathWindWalker is null || DeathWindWalker.IsPublishedCurrentPlain()) &&
            (DeathNotifications is null || DeathNotifications.IsPublishedCurrentPlain()) &&
            NativeMatches(ActorBinding, Actor, ActorScalars.Value, ActorAction.Value, ActorStamina) &&
            NativeMatches(TargetBinding, Target, TargetScalars.Value, TargetAction.Value, TargetStamina);

        private static bool NativeMatches(LegacyOrdinarySpellBinding binding, WorldSession after,
            LegacyNativeActorScalarState.Values scalar, uint action, ushort stamina) =>
            binding.Player.HitPoints == after.Character.MinHitPoints && binding.Player.Mana == after.Character.MinMana &&
            binding.Player.Stamina == stamina && (uint)binding.Player.Action == action &&
            binding.Player.Reborn == scalar.Reborn && binding.Player.Agility == scalar.Agility &&
            binding.Player.X == scalar.X && binding.Player.Y == scalar.Y &&
            binding.Player.Map == scalar.Map && binding.Player.DynamicID == scalar.DynamicID;
    }
    private readonly ConditionalWeakTable<BasicPhysicalPublication, BasicPhysicalPublicationData> basicPhysicalPublications = new();

    private bool IsBasicPhysicalPublicationCurrent(BasicPhysicalPublicationData actual)
    {
        if (actual.CommittedWriter?.IsCommitted != true) { actual.OutputPhase += ":writer"; return false; }
        var actor = actual.ActorBinding; var target = actual.TargetBinding;
        lock (actual.Frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, actual.Actor, actual.Target);
            if (!_connectionGates.TryGetValue(actor.Connection, out var ownGate) ||
                !_connectionGates.TryGetValue(target.Connection, out var peerGate)) return false;
            bool ownFirst = GetConnectionGateOrder(actor.Connection) < GetConnectionGateOrder(target.Connection);
            lock (ownFirst ? ownGate : peerGate)
            lock (ownFirst ? peerGate : ownGate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!actual.Frame.IsActionLifecycleCurrent(this, actual.Context) ||
                    !_sessions.TryGetValue(actor.Connection, out var own) || !ReferenceEquals(own.Read(), actual.Actor) ||
                    !_sessions.TryGetValue(target.Connection, out var peer) || !ReferenceEquals(peer.Read(), actual.Target) ||
                    !ReferenceEquals(own.SpellBinding, actor) || !ReferenceEquals(peer.SpellBinding, target))
                { actual.OutputPhase += ":holders"; return false; }
                using var first = new SpellActorScope(ownFirst ? actor : target);
                using var second = new SpellActorScope(ownFirst ? target : actor);
                lock ((ownFirst ? actor : target).Player.BitVector.SyncRoot)
                lock ((ownFirst ? target : actor).Player.BitVector.SyncRoot)
                {
                    if (!BindingIsCurrentPlain(actor, own, actual.Actor) || !BindingIsCurrentPlain(target, peer, actual.Target))
                    { actual.OutputPhase += ":binding"; return false; }
                    using var firstItems = (ownFirst ? actor : target).Items.EnterCurrentReadScope();
                    using var secondItems = (ownFirst ? target : actor).Items.EnterCurrentReadScope();
                    if (!actual.FinalInputsCurrentPlain()) { actual.OutputPhase += ":inputs"; return false; }
                    if (!actual.Frame.IsBasicPhysicalPublicationGenerationCurrent(actual.Plan.Context, actual.Plan.Decision, actual.NativeActor))
                    { actual.OutputPhase += ":generation"; return false; }
                    return true;
                }
            }
        }
    }
}
