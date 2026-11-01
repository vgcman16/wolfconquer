namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsBasicPhysicalPreparedProficiencyOwnerCurrent(WorldServerFrameHandler frame,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit) =>
        ReferenceEquals(framePersistenceOwner, frame) && ReferenceEquals(hit.Context.Sessions, this) &&
        ReferenceEquals(hit.ActorBinding.Store, this) && ReferenceEquals(hit.TargetBinding.Store, this) &&
        frame.IsActionLifecycleCurrent(this, hit.Context) && hit.IsCurrentPlain();

    internal sealed partial class BasicPhysicalPublication
    {
        // Called only under the same actual two connection/native actor scopes
        // as the concrete progression writer. No higher lock or I/O is taken.
        internal bool IsCurrentProficiencyWritePlain(LegacyPreparedBasicPhysicalProficiencyWrite writer,
            out WorldSession actor)
        {
            actor = null!;
            if (!store.basicPhysicalPublications.TryGetValue(this, out var actual) ||
                !ReferenceEquals(actual.ProficiencyWrite, writer) || !writer.IsPreparedFor(actual.Plan) ||
                !actual.BaseOutputCompleted || actual.ProficiencyCompleted ||
                !store.IsBasicPhysicalPublicationCurrentHeldPlain(actual)) return false;
            actor = actual.Actor;
            return writer.Plan.MatchesSource(actor);
        }
    }

    private bool IsBasicPhysicalPublicationCurrentHeldPlain(BasicPhysicalPublicationData actual)
    {
        var actor = actual.ActorBinding; var target = actual.TargetBinding;
        return actual.CommittedWriter?.IsCommitted == true && IsCurrentThreadInsideAnyConnectionGate &&
            ReferenceEquals(framePersistenceOwner, actual.Frame) && actual.Frame.IsActionLifecycleCurrent(this, actual.Context) &&
            _connectionGates.TryGetValue(actor.Connection, out var actorGate) && Monitor.IsEntered(actorGate) &&
            _connectionGates.TryGetValue(target.Connection, out var targetGate) && Monitor.IsEntered(targetGate) &&
            Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(target.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(target.Player.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(target.Player.BitVector.SyncRoot) &&
            _sessions.TryGetValue(actor.Connection, out var own) && ReferenceEquals(own.Read(), actual.Actor) &&
            _sessions.TryGetValue(target.Connection, out var peer) && ReferenceEquals(peer.Read(), actual.Target) &&
            BindingIsCurrentPlain(actor, own, actual.Actor) && BindingIsCurrentPlain(target, peer, actual.Target) &&
            actual.FinalInputsCurrentPlain() && actual.Frame.IsBasicPhysicalPublicationGenerationCurrent(actual.Plan.Context,
                actual.Plan.Decision, actual.NativeActor);
    }

    internal bool IsActualBasicPhysicalProficiencyPublished(BasicPhysicalPublication publication,
        LegacyPreparedBasicPhysicalProficiencyWrite writer, WorldSession previous, WorldSession current) =>
        basicPhysicalPublications.TryGetValue(publication, out var actual) &&
        ReferenceEquals(actual.ProficiencyWrite, writer) && writer.IsCommittedFor(publication) && actual.ProficiencyCompleted &&
        ReferenceEquals(actual.ActorBeforeProficiency, previous) && ReferenceEquals(actual.Actor, current) &&
        IsBasicPhysicalPublicationCurrentHeldPlain(actual);
}
