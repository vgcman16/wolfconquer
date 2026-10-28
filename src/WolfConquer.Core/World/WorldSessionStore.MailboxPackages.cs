using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private LegacyMailboxPackageProducer? _mailboxPackages;
    internal LegacyMailboxPackageProducer? MailboxPackages => Volatile.Read(ref _mailboxPackages);

    internal void BindMailboxPackages(LegacyMailboxPackageProducer producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (!producer.IsCurrent(producer.UsersRoot, producer.ItemsRoot, producer.MailboxRoot, producer.Recovery))
            throw new InvalidOperationException("Mailbox package producer is not current.");
        var previous = Interlocked.CompareExchange(ref _mailboxPackages, producer, null);
        if (previous is not null && !ReferenceEquals(previous, producer))
            throw new InvalidOperationException("Store already owns a different mailbox producer.");
        producer.NotifyStoreBinding();
    }

    internal bool TrySetMailboxRecoveryOwner(ConquerTcpConnection connection, WorldSession candidate,
        LegacyMailboxPackageProducer producer, out WorldSession? admitted, out string reason)
    {
        admitted = null;
        RefuseBoundSpellReentry(connection);
        reason = "mailboxClaimRecoveryRequired:packageReadOnlyAdmission";
        var users = candidate.UsersRoot;
        if (users is null || !LegacySessionStorageRoot.TryNormalize(users, out var normalizedUsers) || normalizedUsers is null ||
            !Path.IsPathFullyQualified(users) || !string.Equals(normalizedUsers, producer.UsersRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { reason = "sessionStorageRootInvalid"; return false; }
        BindMailboxPackages(producer);
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(producer, candidate);
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            var holder = _sessions.GetValue(connection, static _ => new SessionHolder());
            if (holder.Read() is not null || connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                candidate.NativeGameClient?.OrdinarySpellBinding is not null ||
                !producer.TryAdmitReadOnlyOwner(candidate, out reason)) return false;
            holder.PublishPrepared(candidate, new object());
            // Deliberately no ordinary persistence callback: this is exact
            // unmodified authenticated recovery loading, not a world mutation.
            admitted = candidate;
            reason = string.Empty;
            return true;
        }
    }

    private WorldSession? PeekMailboxSession(ConquerTcpConnection connection) =>
        _sessions.TryGetValue(connection, out var holder) ? holder.Read() : null;
}
