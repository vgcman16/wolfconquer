using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static readonly StringComparer JournalDomainComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, object> JournalDomains = new(JournalDomainComparer);
    [ThreadStatic] private static string? activeJournalMutation;

    private static string JournalDomainKey(string users) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(users));

    private static bool RefuseJournalReentry(ref string reason)
    {
        if (activeJournalMutation is null) return false;
        reason = "tradeJournalInputInvalid:nestedJournalMutation";
        return true;
    }

    // Mutation scopes enter AFTER complete snapshot monitors. Read probes use
    // only J. Neither scope may request higher/new snapshot locks while held.
    private sealed class JournalDomainScope : IDisposable
    {
        private readonly object gate;
        private readonly bool mutation;
        private bool disposed;

        internal JournalDomainScope(string users, bool mutation)
        {
            var key = JournalDomainKey(users);
            if (activeJournalMutation is { } active &&
                (mutation || !JournalDomainComparer.Equals(active, key)))
                throw new IOException("Journal mutation nesting or cross-domain probe is not permitted.");
            gate = JournalDomains.GetOrAdd(key, static _ => new object());
            this.mutation = mutation;
            Monitor.Enter(gate);
            if (mutation) activeJournalMutation = key;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (mutation) activeJournalMutation = null;
            Monitor.Exit(gate);
        }
    }
}
