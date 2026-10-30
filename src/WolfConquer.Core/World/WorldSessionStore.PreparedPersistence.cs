using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The same actual gate/CAS as TryCommitSession, for a producer that needs its
    // durable UID receipt to finish preparing the immutable final session.
    internal bool TryCommitPreparedSession(ConquerTcpConnection connection, WorldSession expected, LegacyMailboxPackageProducer producer,
        Func<PreparedMailboxItemPublication, WorldSession?> prepareAndPersist)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(prepareAndPersist);
        RefuseBoundSpellReentry(connection);
        if (!ReferenceEquals(MailboxPackages, producer)) return false;
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(producer, expected);
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected)) return false;
            // The final item UIDs come from the genuine package receipt. The
            // closed capability prepares that complete graph before its joint
            // writer, while retaining these same Store/actor owners throughout.
            using var publication = new PreparedMailboxItemPublication(this, connection, producer, expected);
            WorldSession? updated;
            try { updated = prepareAndPersist(publication); }
            catch (LegacyCommittedSpellPublicationException)
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSpellPublicationUnproven");
                FailCommittedSpellPublication(holder, publication.Binding); throw;
            }
            if (updated is null) return false;
            if (updated.Character.MinHitPoints != expected.Character.MinHitPoints ||
                updated.Character.MinMana != expected.Character.MinMana ||
                !ReferenceEquals(updated.PlayerSpells, expected.PlayerSpells) ||
                !ReferenceEquals(updated.PlayerProficiencies, expected.PlayerProficiencies) ||
                !string.Equals(updated.UsersRoot, expected.UsersRoot, StringComparison.Ordinal) ||
                updated.AccountId != expected.AccountId || updated.Character.Uid != expected.Character.Uid ||
                !ReferenceEquals(updated.NativeGameClient, expected.NativeGameClient) || !publication.IsCurrentPlain(updated))
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSpellPublicationUnproven");
                FailCommittedSpellPublication(holder, publication.Binding);
            }
            publication.PublishNative();
            publication.PublishHolder();
            if (!producer.TryFinishSessionPublication(this, connection, updated))
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSessionPublicationUnproven");
                FailCommittedSpellPublication(holder, publication.Binding);
            }
            return true;
        }
    }
}
