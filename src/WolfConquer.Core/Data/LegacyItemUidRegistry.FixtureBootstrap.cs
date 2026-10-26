namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Reads the real freshly bootstrapped leased state. This does not issue an
    // owner or derive authority from a guessed domain/file name.
    internal Guid ReadSyntheticFixtureBootstrapDomain()
    {
        lock (gate)
        {
            if (disposed || blocked is not null || state.Generation != 0 || state.HighWater != state.HistoricalFloor ||
                state.Operations is { Length: > 0 } || state.ProducerIntents is { Length: > 0 } ||
                state.ArchiveHead is not null || state.ArchiveCount is > 0)
                throw new InvalidOperationException("A fresh unallocated fixture bootstrap is required.");
            return Guid.ParseExact(state.Domain, "N");
        }
    }
}
