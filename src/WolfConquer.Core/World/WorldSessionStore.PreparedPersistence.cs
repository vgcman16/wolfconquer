using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The same actual gate/CAS as TryCommitSession, for a producer that needs its
    // durable UID receipt to finish preparing the immutable final session.
    internal bool TryCommitPreparedSession(ConquerTcpConnection connection, WorldSession expected, LegacyMailboxPackageProducer producer,
        Func<WorldSession?> prepareAndPersist)
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
            // The sole package producer proves exact spell-reference retention
            // before its real reservation/intent. No late map preparation occurs.
            using var spells = new PreparedSessionSpellPublication(this, connection, holder, expected, expected);
            spells.ArmFinalProof();
            if (!spells.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
            WorldSession? updated;
            try { updated = prepareAndPersist(); }
            catch (LegacyCommittedSpellPublicationException)
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSpellPublicationUnproven");
                FailCommittedSpellPublication(holder, spells.Binding); throw;
            }
            if (updated is null) return false;
            if (updated.Character.MinHitPoints != expected.Character.MinHitPoints ||
                !ReferenceEquals(updated.PlayerSpells, expected.PlayerSpells) ||
                !ReferenceEquals(updated.PlayerProficiencies, expected.PlayerProficiencies) ||
                !string.Equals(updated.UsersRoot, expected.UsersRoot, StringComparison.Ordinal) ||
                updated.AccountId != expected.AccountId || updated.Character.Uid != expected.Character.Uid ||
                !ReferenceEquals(updated.NativeGameClient, expected.NativeGameClient) || !spells.IsCurrentPlain())
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSpellPublicationUnproven");
                FailCommittedSpellPublication(holder, spells.Binding);
            }
            spells.PublishNative();
            holder.PublishPrepared(updated, holder.NativeClientIdentity);
            if (!producer.TryFinishSessionPublication(this, connection, updated))
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSessionPublicationUnproven");
                FailCommittedSpellPublication(holder, spells.Binding);
            }
            return true;
        }
    }
}
