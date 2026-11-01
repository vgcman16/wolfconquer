using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitPairMovement(WorldPacketHandlerContext context, WorldActiveSession partner,
        LegacyPairMovementKind kind, ushort x, ushort y, ushort directionOrFacing,
        out PairMovementPublication? receipt)
    {
        receipt = null;
        RefuseBoundSpellReentry(context.Connection); RefuseBoundSpellReentry(partner.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } expected ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual) ||
            ReferenceEquals(context.Connection, partner.Connection)) return false;
        context = actual;
        WorldSession ownFinal, peerFinal;
        LegacyOrdinarySpellBinding own, peer;
        PairMovementPublication prepared;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, partner.Session);
            if (!_connectionGates.TryGetValue(context.Connection, out var ownGate) ||
                !_connectionGates.TryGetValue(partner.Connection, out var peerGate)) return false;
            bool ownFirst = GetConnectionGateOrder(context.Connection) < GetConnectionGateOrder(partner.Connection);
            lock (ownFirst ? ownGate : peerGate)
            lock (ownFirst ? peerGate : ownGate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), expected) ||
                    ownHolder.SpellBinding is not { } ownBinding || !BindingIsCurrentPlain(ownBinding, ownHolder, expected) ||
                    !_sessions.TryGetValue(partner.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), partner.Session) ||
                    peerHolder.SpellBinding is not { } peerBinding || !BindingIsCurrentPlain(peerBinding, peerHolder, partner.Session) ||
                    ReferenceEquals(ownBinding.Client, peerBinding.Client)) return false;
                own = ownBinding; peer = peerBinding;
                using var firstActors = new SpellActorScope(ownFirst ? own : peer);
                using var secondActors = new SpellActorScope(ownFirst ? peer : own);
                if (!BindingIsCurrentPlain(own, ownHolder, expected) || !BindingIsCurrentPlain(peer, peerHolder, partner.Session) ||
                    !frame.IsPairMovementContextCurrent(this, context, expected, partner.Session) ||
                    !own.Recovery.IsReadyForPairMovement(own.UsersRoot)) return false;
                var operation = new LegacyPairMovementOperation(kind, own.AccountId, checked((uint)own.Uid),
                    peer.AccountId, checked((uint)peer.Uid), own.Player.Map, own.Player.DynamicID,
                    own.Player.X, own.Player.Y, x, y, directionOrFacing);
                var writer = LegacyPreparedPairMovementWrite.Prepare(frame, this, context, operation, own, expected, peer, partner.Session);
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, writer.ActorProposed) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, partner.Session, writer.PartnerProposed)) return false;
                using var ownPublication = new PreparedSessionSpellPublication(this, own.Connection, ownHolder, expected,
                    writer.ActorProposed, pairMovementPublication: writer.PrepareActorPublication());
                using var peerPublication = new PreparedSessionSpellPublication(this, peer.Connection, peerHolder, partner.Session,
                    writer.PartnerProposed, pairMovementPublication: writer.PreparePartnerPublication());
                ownFinal = ownPublication.FinalSession; peerFinal = peerPublication.FinalSession;
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, own, expected);
                prepared = new(this, frame, context, own, peer, ownFinal, peerFinal, operation);
                ownPublication.ArmFinalProof(); peerPublication.ArmFinalProof();
                if (!writer.IsCurrent() || !cancellation.IsCurrentPlain() ||
                    !ownPublication.ConsumeBeforeWriterPlain() || !peerPublication.ConsumeBeforeWriterPlain())
                    throw InvalidSpellPublication();
                var outcome = writer.TryCommit(context.PairMovementJournalInterruption, out var reason);
                if (outcome == LegacyPreparedPairMovementWrite.Outcome.Rejected) return false;
                if (outcome == LegacyPreparedPairMovementWrite.Outcome.RecoveryRequired)
                {
                    var uncertain = new LegacyPairMovementRecoveryRequiredException(reason);
                    try { own.Recovery.Block("pairMovementRecoveryRequired:" + reason); }
                    catch (Exception fence) { throw new AggregateException("Pair movement persistence and fencing failed.", uncertain, fence); }
                    finally { ownHolder.Clear(); peerHolder.Clear(); }
                    throw uncertain;
                }
                if (!ownPublication.AcceptExternallyPersistedHitpoints() || !peerPublication.AcceptExternallyPersistedHitpoints() ||
                    !ownPublication.IsCurrentPlain() || !peerPublication.IsCurrentPlain() || !cancellation.IsCurrentPlain())
                    FailCommittedPairPublication(ownHolder, own, peerHolder, peer);
                ownPublication.PublishNative(); peerPublication.PublishNative();
                cancellation.PublishPrepared();
                ownPublication.PublishHolder(); peerPublication.PublishHolder();
                prepared.ConfirmCommitted();
                ownPublication.Dispose(); peerPublication.Dispose();
                try
                {
                    PersistFrameOwnedSessionChange(own.Connection, expected, ownFinal, own.NativeIdentity, own, ownPublication.HitpointsWrite);
                    PersistFrameOwnedSessionChange(peer.Connection, partner.Session, peerFinal, peer.NativeIdentity, peer, peerPublication.HitpointsWrite);
                }
                catch (WorldSessionPublicationException) { ownHolder.Clear(); peerHolder.Clear(); throw; }
            }
        }
        try
        {
            NotifyPublishedSessionChange(own.Connection, expected, ownFinal, own.NativeIdentity, own);
            NotifyPublishedSessionChange(peer.Connection, partner.Session, peerFinal, peer.NativeIdentity, peer);
        }
        catch (WorldSessionPublicationException failure) { prepared.FenceBoth(failure); throw; }
        receipt = prepared;
        return true;
    }
}

internal sealed class LegacyPairMovementRecoveryRequiredException(string reason) : Exception(reason) { }
