namespace WolfConquer.Core.World;

internal static partial class LegacyMailboxPackageMutationFence
{
    private static readonly object StoreMutationGate = new();
    private sealed class StoreScope(bool entered) : IDisposable
    { public void Dispose() { if (entered) Monitor.Exit(StoreMutationGate); } }

    internal static IDisposable EnterStoreScope(LegacyMailboxPackageProducer? bound, params WorldSession?[] sessions)
    {
        var roots = new List<string>();
        foreach (var session in sessions)
        {
            if (!LegacySessionStorageRoot.TryNormalize(session?.UsersRoot, out var root))
                throw new InvalidOperationException("sessionStorageRootInvalid");
            if (root is not null) roots.Add(root);
        }
        var owners = Snapshot();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var needed = bound is not null || roots.Any(root => owners.Any(owner =>
            string.Equals(root, owner.UsersRoot, comparison)));
        if (!needed) return new StoreScope(false);
        // All Store entry points, including the native identity callback owner,
        // enter here BEFORE a connection gate. Never invert that order through
        // a newly-bound/reentrant callback.
        if (WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate && !Monitor.IsEntered(StoreMutationGate))
            throw new InvalidOperationException("Package Store mutation gate order is invalid.");
        Monitor.Enter(StoreMutationGate);
        return new StoreScope(true);
    }
}
