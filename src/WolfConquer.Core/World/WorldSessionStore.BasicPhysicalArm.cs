namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryArmCompletedBasicPhysical(BasicPhysicalPublication token)
    {
        if (!basicPhysicalPublications.TryGetValue(token, out var actual) || !actual.CompletedOutput ||
            !actual.ProficiencyCompleted || !actual.ProficiencyWrite.IsCommittedFor(token)) return false;
        var actor = actual.ActorBinding; var target = actual.TargetBinding;
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? armed = null;
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
                using var first = new SpellActorScope(ownFirst ? actor : target);
                using var second = new SpellActorScope(ownFirst ? target : actor);
                lock ((ownFirst ? actor : target).Player.BitVector.SyncRoot)
                lock ((ownFirst ? target : actor).Player.BitVector.SyncRoot)
                {
                    if (!IsBoundSpellOwnerCurrent(actor) || !IsBoundSpellOwnerCurrent(target)) return false;
                    using var firstItems = (ownFirst ? actor : target).Items.EnterCurrentReadScope();
                    using var secondItems = (ownFirst ? target : actor).Items.EnterCurrentReadScope();
                    if (!IsBasicPhysicalPublicationCurrentHeldPlain(actual)) return false;
                    // A completed lethal hit remains committed without arming
                    // against an actual dead participant.
                    if (!actor.Player.Alive || !target.Player.Alive || actual.Target.Character.MinHitPoints == 0) return true;
                    if (!actual.Frame.TryReadBasicPhysicalArmGeneration(this, actual.Context, actual.Plan.Decision,
                            actual.NativeActor, out var generation)) return false;
                    var change = IssueBasicPhysicalChange(actual.Frame, actor, actual.Actor, actual.NativeActor, generation,
                        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.ArmRepeat, actual.Plan.Response.Query, null, actual.Context);
                    var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(actor, change);
                    var runtime = actual.Frame.PrepareBasicPhysicalNativePublication(this, actor, change, native, actual.Context.BasicPhysicalAttempt);
                    if (!IsBasicPhysicalPublicationCurrentHeldPlain(actual) || !native.IsCurrentPlain() || !runtime.IsCurrentPlain()) return false;
                    native.PublishPrepared(); runtime.PublishPrepared();
                    armed = native.After;
                }
            }
        }
        if (armed is not null) actual.Frame.NotifyActualBasicPhysicalArm(this, actor, armed);
        return armed is not null;
    }
}
