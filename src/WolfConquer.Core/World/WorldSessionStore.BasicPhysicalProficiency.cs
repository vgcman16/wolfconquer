using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryCommitBasicPhysicalProficiency(BasicPhysicalPublication token)
    {
        if (!basicPhysicalPublications.TryGetValue(token, out var actual)) return false;
        var frame = actual.Frame; var actor = actual.ActorBinding; var target = actual.TargetBinding;
        WorldSession? previous = null, published = null;
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
                    if (!token.IsCurrentProficiencyWritePlain(actual.ProficiencyWrite, out var expected)) return false;
                    if (!actual.ProficiencyWrite.Plan.HasChanges)
                    {
                        if (actual.ProficiencyWrite.TryCommit(token, out _) != LegacyPreparedBasicPhysicalProficiencyWrite.Outcome.Committed)
                            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalNeutralProficiencyOwnerLost");
                        actual.ActorBeforeProficiency = actual.Actor;
                        actual.ProficiencyCompleted = true;
                        return true;
                    }
                    if (!_sessions.TryGetValue(actor.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected)) return false;
                    previous = expected;
                    published = CommitBasicPhysicalProficiencyHeld(token, actual, holder, expected);
                }
            }
        }
        try { NotifyPublishedSessionChange(actor.Connection, previous, published!, actor.NativeIdentity, actor); }
        catch (Exception error) { throw FenceBasicPhysicalPersistence(actual, "basicPhysicalProficiencyObserverFailed", error); }
        return true;
    }

    private WorldSession CommitBasicPhysicalProficiencyHeld(BasicPhysicalPublication token,
        BasicPhysicalPublicationData actual, SessionHolder holder, WorldSession expected)
    {
        var actor = actual.ActorBinding;
        using var publication = new PreparedSessionSpellPublication(this, actor.Connection, holder, expected,
            expected with { PlayerProficiencies = actual.ProficiencyWrite.Plan.After });
        var final = publication.FinalSession;
        if (!ReferenceEquals(expected.Character, final.Character) ||
            (final with { PlayerItems = expected.PlayerItems, PlayerSpells = expected.PlayerSpells,
                PlayerProficiencies = expected.PlayerProficiencies }) != expected ||
            !ReferenceEquals(final.PlayerProficiencies, actual.ProficiencyWrite.Plan.After) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(final.PlayerItems)) ||
            !PlayerSpellSnapshotLoader.ToLegacyBytes(expected.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(final.PlayerSpells)))
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalProficiencyProjectionChangedOtherState");
        publication.ArmFinalProof();
        if (!publication.ConsumeBeforeWriterPlain() || !token.IsCurrentProficiencyWritePlain(actual.ProficiencyWrite, out _))
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalProficiencyBeforeProofLost");
        var outcome = actual.ProficiencyWrite.TryCommit(token, out var reason);
        if (outcome != LegacyPreparedBasicPhysicalProficiencyWrite.Outcome.Committed)
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalCommittedHitProficiencyUnsettled:" + reason);
        if (!actual.ProficiencyWrite.ExactCommittedImageIsCurrent(token) || !publication.IsCurrentPlain() ||
            !token.IsCurrentProficiencyWritePlain(actual.ProficiencyWrite, out _))
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalCommittedProficiencyPublicationUnproven");
        publication.PublishNative(); publication.PublishHolder();
        actual.ActorBeforeProficiency = expected; actual.Actor = final;
        actual.ActorSpellGraph = publication.PreparedSpellGraph;
        actual.ActorProficiencyGraph = publication.PreparedProficiencyGraph;
        actual.ProficiencyCompleted = true;
        actual.ProficiencyWrite.RegisterPublished(token, expected, final);
        publication.Dispose();
        PersistFrameOwnedSessionChange(actor.Connection, expected, final, actor.NativeIdentity, actor,
            completedProficiencies: actual.ProficiencyWrite);
        return final;
    }
}
