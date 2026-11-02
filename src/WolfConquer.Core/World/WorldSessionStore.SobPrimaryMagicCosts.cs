using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitSobPrimaryMagicCosts(ConquerTcpConnection connection, WorldSession expected,
        object identity, WorldSession candidate, WorldPacketHandlers.LegacySobPrimaryAdmission admission,
        out MagicCostOutputReceipt? receipt, LegacyRuneCastEligibility? runeEligibility)
    {
        receipt = null;
        RefuseBoundSpellReentry(connection);
        MagicCostOutputReceipt prepared;
        WorldSession final; object publishedIdentity; LegacyOrdinarySpellBinding? binding;
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, candidate);
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!TryCurrentClassicParticipant(new(connection, expected, identity), out var holder) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, candidate) ||
                    !admission.TryReadTarget(this, connection, expected, identity, out var target) || target is null) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, candidate);
                prepared = new(this, connection, publication.PublishedIdentity, publication.Binding, expected,
                    ReferenceEquals(expected, candidate) ? expected : publication.FinalSession);
                if (ReferenceEquals(expected, candidate))
                {
                    if (!admission.TryEnterForCost(this, connection, expected, out var lease, out _)) return false;
                    using (lease)
                    {
                        publication.ArmFinalProof();
                        if (!publication.ConsumeBeforeWriterPlain() || !publication.IsCurrentPlain() || !lease!.IsCurrentPlain())
                            throw InvalidSpellPublication();
                        if (runeEligibility is not null && !runeEligibility.IsCurrentPlain()) return false;
                        receipt = prepared;
                        return true;
                    }
                }
                if (!TryPublishFrameOwnedHitpoints(publication, holder, out _, runeEligibility: runeEligibility,
                        sobPrimary: admission)) return false;
                final = publication.FinalSession; publishedIdentity = publication.PublishedIdentity; binding = publication.Binding;
                publication.Dispose();
                PersistFrameOwnedSessionChange(connection, expected, final, publishedIdentity, binding, publication.HitpointsWrite);
            }
        }
        receipt = prepared;
        NotifyPublishedSessionChange(connection, expected, final, publishedIdentity, binding);
        return true;
    }
}
