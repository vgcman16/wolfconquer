using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitClassicBuffCosts(ConquerTcpConnection connection, WorldSession expected,
        object identity, WorldSession candidate, ProcParticipant? peer,
        MonsterRuntimeStateStore.ClassicBuffTarget? monster, out MagicCostOutputReceipt? receipt, out string reason)
    {
        receipt = null; reason = "classicTargetOwnerChanged";
        RefuseBoundSpellReentry(connection);
        if (peer is { } other)
        {
            RefuseBoundSpellReentry(other.Connection);
            if (ReferenceEquals(connection, other.Connection)) return false;
        }
        WorldSession final; object finalIdentity; LegacyOrdinarySpellBinding? binding;
        MagicCostOutputReceipt prepared;
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, candidate, peer?.Expected);
            var ownGate = _connectionGates.GetValue(connection, static _ => new object());
            var peerGate = peer is { } observed ? _connectionGates.GetValue(observed.Connection, static _ => new object()) : ownGate;
            var ownerFirst = peer is not { } ordered || GetConnectionGateOrder(connection) < GetConnectionGateOrder(ordered.Connection);
            lock (ownerFirst ? ownGate : peerGate)
            lock (ownerFirst ? peerGate : ownGate)
            {
                using var scope = EnterNativeConnectionGate();
                if (!TryCurrentClassicParticipant(new(connection, expected, identity), out var holder) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, candidate)) return false;
                SessionHolder? peerHolder = null;
                if (peer is { } participant && (!TryCurrentClassicParticipant(participant, out peerHolder) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, participant.Expected, participant.Expected))) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, candidate);
                using var observation = peer is { } selected ? new PreparedSessionSpellPublication(this,
                    selected.Connection, peerHolder!, selected.Expected, selected.Expected) : null;
                if (publication.Binding is { } own && observation?.Binding is { } otherBinding &&
                    ReferenceEquals(own.Client, otherBinding.Client)) throw InvalidSpellPublication();
                prepared = new(this, connection, publication.PublishedIdentity, publication.Binding, expected, ReferenceEquals(expected, candidate) ? expected : publication.FinalSession);
                MonsterRuntimeStateStore.ClassicBuffLease? lease = null;
                if (ReferenceEquals(expected, candidate) && monster is not null && !monster.TryEnter(out lease, out reason)) return false;
                using (lease)
                {
                    if (ReferenceEquals(expected, candidate))
                    {
                        publication.ArmFinalProof(); observation?.ArmFinalProof();
                        if (!publication.ConsumeBeforeWriterPlain() || observation is not null && !observation.ConsumeBeforeWriterPlain() ||
                            !publication.IsCurrentPlain() || observation is not null && !observation.IsCurrentPlain() ||
                            lease is not null && !lease.IsCurrentPlain()) throw InvalidSpellPublication();
                        receipt = prepared; reason = string.Empty; return true;
                    }
                    if (!TryPublishFrameOwnedHitpoints(publication, holder, out reason, observation, peerHolder, monster)) return false;
                }
                final = publication.FinalSession; finalIdentity = publication.PublishedIdentity; binding = publication.Binding;
                observation?.Dispose(); publication.Dispose();
                PersistFrameOwnedSessionChange(connection, expected, final, finalIdentity, binding, publication.HitpointsWrite);
            }
        }
        receipt = prepared; reason = string.Empty;
        NotifyPublishedSessionChange(connection, expected, final, finalIdentity, binding);
        return true;
    }

    private bool TryCurrentClassicParticipant(ProcParticipant participant, out SessionHolder holder)
    {
        if (!TryCurrentProcParticipant(participant, out holder)) return false;
        return holder.SpellBinding is not null ||
            participant.Connection.NativeGameClient?.OrdinarySpellBinding is null &&
            participant.Connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment != true;
    }
}
