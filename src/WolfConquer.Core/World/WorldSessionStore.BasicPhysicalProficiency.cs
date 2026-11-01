namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryCommitBasicPhysicalProficiency(BasicPhysicalPublication token)
    {
        if (!basicPhysicalPublications.TryGetValue(token, out var actual)) return false;
        if (actual.JointProgressionWriter is not null)
            return actual.ProficiencyCompleted && actual.ProficiencyWrite.IsCommittedFor(token) &&
                actual.ProficiencyWrite.ExactCommittedImageIsCurrent(token) && IsBasicPhysicalPublicationCurrent(actual);
        var frame = actual.Frame; var actor = actual.ActorBinding; var target = actual.TargetBinding;
        lock (frame.RequireActionLifecycleGate(this))
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
                    if (!token.IsCurrentProficiencyWritePlain(actual.ProficiencyWrite, out _)) return false;
                    if (!actual.ProficiencyWrite.Plan.HasChanges)
                    {
                        if (actual.ProficiencyWrite.TryCommit(token, out _) != LegacyPreparedBasicPhysicalProficiencyWrite.Outcome.Committed)
                            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalNeutralProficiencyOwnerLost");
                        actual.ActorBeforeProficiency = actual.Actor;
                        actual.ProficiencyCompleted = true;
                        return true;
                    }
                    throw FenceBasicPhysicalPersistence(actual, "basicPhysicalChangedProficiencyMissingJointWriter");
                }
            }
        }
    }

}
