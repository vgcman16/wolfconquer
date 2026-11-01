namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitOwnedBasicPhysical(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession selected, out BasicPhysicalPublication? publication, out string reason)
    {
        publication = null; reason = "basicPhysicalCurrentOwnerUnavailable";
        RefuseBoundSpellReentry(context.Connection); RefuseBoundSpellReentry(selected.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } expected ||
            !frame.TryPrepareBasicPhysicalEntityContext(this, context, expected, out var actual) ||
            actual.BasicPhysicalAttempt is not { } attempt) return false;
        context = actual;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, selected.Session);
            if (!frame.TryGetBasicPhysicalAttemptRoles(context, attempt, out var actor, out var target) ||
                !ReferenceEquals(target.Connection, selected.Connection) ||
                !_connectionGates.TryGetValue(actor.Connection, out var ownGate) ||
                !_connectionGates.TryGetValue(target.Connection, out var peerGate)) return false;
            bool ownFirst = GetConnectionGateOrder(actor.Connection) < GetConnectionGateOrder(target.Connection);
            lock (ownFirst ? ownGate : peerGate)
            lock (ownFirst ? peerGate : ownGate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(actor.Connection, out var own) || !ReferenceEquals(own.Read(), expected) ||
                    !_sessions.TryGetValue(target.Connection, out var peer) || !ReferenceEquals(peer.Read(), selected.Session) ||
                    !BindingIsCurrentPlain(actor, own, expected) || !BindingIsCurrentPlain(target, peer, selected.Session)) return false;
                using var first = new SpellActorScope(ownFirst ? actor : target);
                using var second = new SpellActorScope(ownFirst ? target : actor);
                lock ((ownFirst ? actor : target).Player.BitVector.SyncRoot)
                lock ((ownFirst ? target : actor).Player.BitVector.SyncRoot)
                    if (!TryCommitOwnedBasicPhysicalHeld(frame, context, packet, selected, actor, target,
                            own, peer, out publication, out reason)) return false;
            }
        }
        if (publication is null || !basicPhysicalPublications.TryGetValue(publication, out var committed))
            throw InvalidSpellPublication();
        try
        {
            NotifyPublishedSessionChange(committed.ActorBinding.Connection, committed.Plan.ExpectedActor, committed.Actor,
                committed.ActorBinding.NativeIdentity, committed.ActorBinding);
            NotifyPublishedSessionChange(committed.TargetBinding.Connection, committed.Plan.ExpectedTarget, committed.Target,
                committed.TargetBinding.NativeIdentity, committed.TargetBinding);
        }
        catch (Exception error) { throw FenceBasicPhysicalPersistence(committed, "basicPhysicalCommittedObserverFailed", error); }
        return true;
    }
}
