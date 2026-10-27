namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    private static readonly object DomainGate = new();
    private static readonly StringComparer DomainComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly Dictionary<string, HashSet<string>> UsersByParticipantDirectory = new(DomainComparer);
    private static readonly Dictionary<string, string> ConfiguredUsers = new(DomainComparer);

    // Knowledge of actual configured routes can only DENY writes. It is neither
    // a UID producer nor permission to commit/recover a journal. Retain prior
    // route knowledge: reconfiguration cannot hide an occupied old shared slot.
    internal static void BindConfiguredDomain(LegacyOrdinaryAdmissionRoots roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var users = CanonicalConfiguredRoot(roots.Users);
        var directories = roots.Kinds.Select(kind => CanonicalConfiguredRoot(roots.Root(kind)!)).Distinct(DomainComparer).ToArray();
        lock (DomainGate)
        {
            ConfiguredUsers.TryAdd(users, users);
            foreach (var directory in directories)
            {
                if (!UsersByParticipantDirectory.TryGetValue(directory, out var members))
                    UsersByParticipantDirectory.Add(directory, members = new(DomainComparer));
                members.Add(users);
            }
        }
    }

    private static string CanonicalConfiguredRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("An explicit absolute admission route is required.");
        var result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!DomainComparer.Equals(path, result)) throw new ArgumentException("Admission routes must be canonical.");
        return result;
    }

    private static void RequireConfiguredJournalWritable(string absolute)
    {
        var directory = Path.GetDirectoryName(absolute);
        if (directory is null) return;
        string[] users;
        lock (DomainGate)
            users = UsersByParticipantDirectory.TryGetValue(directory, out var matches) ? matches.ToArray() : [];
        // Only matching configured domains perform filesystem inspection. An
        // unrelated world's retained binding cannot cause scans on this save.
        foreach (var root in users)
            if (LegacyTradeJournal.HasPending(root))
                throw new IOException("ordinaryAdmissionRecoveryRequired:pendingConfiguredJournalWrite");
    }

    // Receives only a successfully normalized root; null alone means storage-free.
    private static bool ConfiguredSessionWritable(string? users)
    {
        if (users is null) return true;
        string? actual;
        lock (DomainGate) ConfiguredUsers.TryGetValue(users, out actual);
        return actual is null || !LegacyTradeJournal.HasPending(actual);
    }
}
