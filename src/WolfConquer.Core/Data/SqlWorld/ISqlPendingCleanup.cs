namespace WolfConquer.Core.Data.SqlWorld;

// Disposal-only ownership shared across raw acquisition and handoff failures.
// It cannot expose or mint authentication, storage, native or publication grants.
internal interface ISqlPendingCleanup : IAsyncDisposable
{
    bool HasPendingCleanup { get; }
}
