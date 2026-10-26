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
        if (IsCurrentThreadInsideAnyConnectionGate) return false;
        if (!ReferenceEquals(MailboxPackages, producer)) return false;
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(producer, expected);
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected)) return false;
            var updated = prepareAndPersist();
            if (updated is null) return false;
            holder.Publish(updated);
            if (!producer.TryFinishSessionPublication(this, connection, updated))
            {
                producer.Recovery.Block("mailboxClaimRecoveryRequired:packageSessionPublicationUnproven");
                throw new InvalidOperationException("mailboxClaimRecoveryRequired:packageSessionPublicationUnproven");
            }
            return true;
        }
    }
}
