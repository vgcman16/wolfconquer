using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, LegacyOrdinaryAdmissionUidOwner> Owners = new(StringComparer.OrdinalIgnoreCase);
    [ThreadStatic] private static LegacyOrdinaryAdmissionPlan? prepared;

    internal static void Register(LegacyOrdinaryAdmissionUidOwner owner)
    { lock (Gate) Owners[owner.Roots.Users] = owner; }
    private static LegacyOrdinaryAdmissionUidOwner[] Snapshot() { lock (Gate) return Owners.Values.ToArray(); }

    internal static void RequireFileMutation(string path) => RequireFileMutation(path, null);

    private static void RequireFileMutation(string path, LegacyTradeJournal.OwnedStartupItemRecovery? recovery)
    {
        var absolute = Path.GetFullPath(path);
        RequireConfiguredJournalWritable(absolute, recovery);
        foreach (var owner in Snapshot())
            foreach (var kind in owner.Roots.Kinds)
            {
                if (!string.Equals(Path.GetDirectoryName(absolute), owner.Roots.Root(kind), StringComparison.OrdinalIgnoreCase)) continue;
                var extension = kind is LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.CombatGear ? ".ini" : ".bin";
                var name = Path.GetFileName(absolute);
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
        foreach (var owner in Snapshot())
            foreach (var (session, users) in new[] { (previous, previousUsers), (next, nextUsers) })
                if (session is not null && session.AccountId is > 0 and <= uint.MaxValue && users is not null &&
                    string.Equals(users, owner.Roots.Users, StringComparison.OrdinalIgnoreCase) &&
                    owner.IsPending((uint)session.AccountId) &&
                    (admission is null || !admission.MatchesPendingOwner(owner, (uint)session.AccountId))) return false;
        return true;
    }

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
