using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Every real Store holder consumes its exact expected record. Detached/no-Store
    // fixtures are handled separately by their limited caller adapter.
    internal bool TryPublishPlayerSpells(ConquerTcpConnection connection, WorldSession expected,
        WorldSession proposed, out WorldSession? final)
    {
        return TryCompareAndSet(connection, expected, proposed, out final);
    }

    internal bool TryUpdateCurrentNativeSession(ConquerTcpConnection connection, long uid, object identity,
        Func<WorldSession, WorldSession> update, out WorldSession? final)
    {
        ArgumentNullException.ThrowIfNull(update);
        final = null;
        RefuseBoundSpellReentry(connection);
        WorldSession? previous;
        object publishedIdentity;
        LegacyOrdinarySpellBinding? publishedBinding;
        {
            using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(connection));
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var scope = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || holder.Read() is not { } current ||
                    current.Character.Uid != uid || !ReferenceEquals(holder.NativeClientIdentity, identity)) return false;
                var proposed = update(current);
                if (!ReferenceEquals(holder.Read(), current) || !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, current, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, current, proposed);
                publication.ArmFinalProof();
                if (!publication.IsCurrentPlain()) throw InvalidSpellPublication();
                publication.PublishNative();
                publication.PublishHolder();
                final = publication.FinalSession;
                publishedIdentity = publication.PublishedIdentity;
                publishedBinding = publication.Binding;
                publication.Dispose();
                previous = current;
                PersistFrameOwnedSessionChange(connection, current, final, publishedIdentity, publishedBinding);
            }
        }
        NotifyPublishedSessionChange(connection, previous, final, publishedIdentity, publishedBinding);
        return true;
    }
}
