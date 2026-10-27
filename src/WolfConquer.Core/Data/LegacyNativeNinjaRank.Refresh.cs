namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    internal static PreparedRefresh PrepareRefresh(uint uid, Entry?[] replacements)
    {
        if (uid == 0 || replacements is null || replacements.Length != (int)Type.Count)
            throw new InvalidOperationException("Ninja refresh requires a positive UID and six category dispositions.");
        for (var index = 0; index < replacements.Length; index++)
            if (replacements[index] is { } entry && (entry.UID != uid || entry.Type != (Type)index))
                throw new InvalidOperationException("Ninja refresh entry identity or category is inconsistent.");

        var prepared = new PreparedRefresh();
        try
        {
            prepared.CaptureAndPrepare(uid, replacements);
            return prepared;
        }
        catch (Exception primary)
        {
            try { prepared.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    internal sealed partial class PreparedRefresh : IDisposable
    {
        private readonly object[] heldGates = new object[(int)Type.Count + 1];
        private readonly int threadId = Environment.CurrentManagedThreadId;
        private int heldCount;
        private bool published;
        private bool disposed;
        private LegacyNativeDbSafeDictionary<Type, Rank> registry = null!;
        private object registryGate = null!;
        private RankSource[] sources = [];
        private Dictionary<uint, Entry>?[] plans = [];

        internal void CaptureAndPrepare(uint uid, Entry?[] replacements)
        {
            registry = Ranks ?? throw UnavailableRefresh();
            registryGate = registry.SyncRoot ?? throw UnavailableRefresh();
            Enter(registryGate);
            var owners = CaptureOwners(registry);
            var gates = owners.Select(owner => owner.SynRoot ?? throw UnavailableRefresh()).ToArray();
            foreach (var gate in gates) Enter(gate);
            sources = owners.Select((owner, index) => CaptureSource(owner, (Type)index, gates[index])).ToArray();
            plans = sources.Select((source, index) => PrepareMap(source, uid, replacements[index])).ToArray();
            RequireCurrentSources();
        }

        internal void Publish()
        {
            RequireCurrentForPublication();
            PublishPreparedMaps();
        }

        internal void RequireCurrentForPublication()
        {
            if (disposed || published || threadId != Environment.CurrentManagedThreadId)
                throw UnavailableRefresh();
            RequireCurrentSources();
        }

        // Both callers hold every captured gate and perform all final checks
        // before their first store. This phase contains stores only.
        internal void PublishPreparedMaps()
        {
            for (var index = 0; index < sources.Length; index++)
                if (plans[index] is { } map) sources[index].Owner.RankingList = map;
            published = true;
        }

        private void Enter(object gate)
        {
            Monitor.Enter(gate);
            heldGates[heldCount++] = gate;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (threadId != Environment.CurrentManagedThreadId) throw UnavailableRefresh();
            List<Exception>? failures = null;
            while (heldCount > 0)
            {
                try { Monitor.Exit(heldGates[--heldCount]); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }
            disposed = true;
            if (failures is not null) throw new AggregateException(failures);
        }
    }

    private static InvalidOperationException UnavailableRefresh() =>
        new("Ninja rank refresh requires a complete, current and valid ranking graph.");
}
