using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    [ThreadStatic] private static LegacyOrdinaryAdmissionPlan? prepared;

    internal static void RequireFileMutation(string path) => RequireFileMutation(path, null);

    private static void RequireFileMutation(string path, LegacyTradeJournal.OwnedStartupItemRecovery? recovery)
    {
        var absolute = Path.GetFullPath(path);
        RequireConfiguredJournalWritable(absolute, recovery);
        var directory = Path.GetDirectoryName(absolute);
        var name = Path.GetFileName(absolute);
        foreach (var participant in SnapshotParticipants(directory))
        {
            var owner = participant.Owner;
            var extension = ParticipantExtension(participant.Kind);
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                !uint.TryParse(name[..^extension.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var account)) continue;
            if (recovery is not null)
            {
                // A foreign live owner is not permission merely because it
                // currently reports no debt on a shared participant path.
                if (!recovery.PermitsOrdinaryOwner(owner, absolute))
                    throw new IOException("ordinaryAdmissionRecoveryRequired:ownedStartupOwnerMismatch");
            }
            else if (owner.IsPending(account) && (prepared is null || !prepared.PermitsOrdinaryPath(owner, account, absolute)))
                throw new IOException("ordinaryAdmissionRecoveryRequired:pendingSnapshotWrite");
        }
    }

    internal static bool CanMutateSession(WorldSession? previous, WorldSession next, LegacyOrdinaryAdmissionPlan? admission)
    {
        if (!LegacySessionStorageRoot.TryNormalize(previous?.UsersRoot, out var previousUsers) ||
            !LegacySessionStorageRoot.TryNormalize(next.UsersRoot, out var nextUsers) ||
            !ConfiguredSessionWritable(previousUsers) || !ConfiguredSessionWritable(nextUsers)) return false;
        var owners = SnapshotSessionOwners(previousUsers, nextUsers);
        return OwnerAllowsSessions(owners.First, previous, previousUsers, next, nextUsers, admission) &&
            OwnerAllowsSessions(owners.Second, previous, previousUsers, next, nextUsers, admission);
    }

    private static bool OwnerAllowsSessions(LegacyOrdinaryAdmissionUidOwner? owner,
        WorldSession? previous, string? previousUsers, WorldSession next, string? nextUsers,
        LegacyOrdinaryAdmissionPlan? admission) => owner is null ||
        (OwnerAllowsSession(owner, previous, previousUsers, admission) &&
         OwnerAllowsSession(owner, next, nextUsers, admission));

    private static bool OwnerAllowsSession(LegacyOrdinaryAdmissionUidOwner owner,
        WorldSession? session, string? users, LegacyOrdinaryAdmissionPlan? admission) =>
        session is null || session.AccountId is <= 0 or > uint.MaxValue || users is null ||
        !string.Equals(users, owner.Roots.Users, StringComparison.OrdinalIgnoreCase) ||
        !owner.IsPending((uint)session.AccountId) ||
        (admission is not null && admission.MatchesPendingOwner(owner, (uint)session.AccountId));

    internal static IDisposable EnterPreparedCommit(LegacyOrdinaryAdmissionPlan plan)
    {
        if (prepared is not null || !plan.IsJournalPrepared) throw new InvalidOperationException("Ordinary admission journal permission is stale.");
        prepared = plan; return new Scope();
    }
    private sealed class Scope : IDisposable { public void Dispose() => prepared = null; }

    internal static void RequireJournalMutation(IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        if (prepared is not null && !prepared.PermitsOrdinaryJournal(paths, changes))
            throw new IOException("ordinaryAdmissionRecoveryRequired:journalMismatch");
        foreach (var path in paths) RequireFileMutation(path);
    }
}
