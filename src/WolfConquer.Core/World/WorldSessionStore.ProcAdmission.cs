using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal readonly record struct ProcParticipant(ConquerTcpConnection Connection, WorldSession Expected, object Identity);

    // A concrete current-record transaction. The peer is observed, not charged;
    // no persistence callback or accepting no-op journal is supplied by callers.
    internal bool TryCommitProc(ProcParticipant owner, WorldSession candidate, ProcParticipant? peer,
        out ProcReceipt? receipt)
    {
        receipt = null;
        RefuseBoundSpellReentry(owner.Connection);
        if (peer is { } other)
        {
            RefuseBoundSpellReentry(other.Connection);
            if (ReferenceEquals(owner.Connection, other.Connection)) return false;
        }
        WorldSession final; object identity; LegacyOrdinarySpellBinding? binding; ProcReceipt prepared;
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages,
                owner.Expected, candidate, peer?.Expected);
            var firstGate = _connectionGates.GetValue(owner.Connection, static _ => new object());
            var secondGate = peer is { } observed ? _connectionGates.GetValue(observed.Connection, static _ => new object()) : firstGate;
            var ownerFirst = peer is not { } ordered || GetConnectionGateOrder(owner.Connection) < GetConnectionGateOrder(ordered.Connection);
            lock (ownerFirst ? firstGate : secondGate)
            lock (ownerFirst ? secondGate : firstGate)
            {
                using var scope = EnterNativeConnectionGate();
                if (!TryCurrentProcParticipant(owner, out var holder) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, owner.Expected, candidate)) return false;
                SessionHolder? peerHolder = null;
                if (peer is { } participant && (!TryCurrentProcParticipant(participant, out peerHolder) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, participant.Expected, participant.Expected))) return false;
                using var publication = new PreparedSessionSpellPublication(this, owner.Connection, holder, owner.Expected, candidate);
                using var observation = peer is { } selected ? new PreparedSessionSpellPublication(this,
                    selected.Connection, peerHolder!, selected.Expected, selected.Expected) : null;
                if (publication.Binding is { } ownBinding && observation?.Binding is { } peerBinding &&
                    ReferenceEquals(ownBinding.Client, peerBinding.Client)) throw InvalidSpellPublication();
                // Allocate the historical receipt before the real writer. It is
                // returned only after commitment and the actual Frame postlude.
                prepared = new(this, owner.Connection, publication.PublishedIdentity, publication.Binding, publication.FinalSession, peer);
                if (!TryPublishFrameOwnedHitpoints(publication, holder, out _, observation, peerHolder)) return false;
                final = publication.FinalSession; identity = publication.PublishedIdentity; binding = publication.Binding;
                observation?.Dispose(); publication.Dispose();
                PersistFrameOwnedSessionChange(owner.Connection, owner.Expected, final, identity, binding, publication.HitpointsWrite);
            }
        }
        receipt = prepared;
        NotifyPublishedSessionChange(owner.Connection, owner.Expected, final, identity, binding);
        return true;
    }

    private bool TryCurrentProcParticipant(ProcParticipant participant, out SessionHolder holder)
    {
        holder = null!;
        return _sessions.TryGetValue(participant.Connection, out holder!) &&
            ReferenceEquals(holder.Read(), participant.Expected) && ReferenceEquals(holder.NativeClientIdentity, participant.Identity) &&
            (holder.SpellBinding is not { } binding || BindingIsCurrentPlain(binding, holder, participant.Expected));
    }
}
