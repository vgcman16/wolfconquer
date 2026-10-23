namespace WolfConquer.Core.Data;

// Inspection prerequisite only. This owner does not admit a journal write or
// export handles/paths as replacement authority.
internal sealed partial class WindowsJournalRootIdentity : IDisposable
{
    private readonly object gate = new();
    private readonly RootCapture[] roots;
    private bool disposed;

    private WindowsJournalRootIdentity(RootCapture[] roots) => this.roots = roots;

    internal static WindowsJournalRootIdentity Capture(string users, string items, string quests)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows root identities are required.");
        var captures = new List<RootCapture>(3);
        try
        {
            captures.Add(CaptureRoot(users));
            captures.Add(CaptureRoot(items));
            captures.Add(CaptureRoot(quests));
            return new(captures.ToArray());
        }
        catch (Exception error)
        {
            for (var index = captures.Count - 1; index >= 0; index--) captures[index].Dispose();
            if (error is DllNotFoundException or EntryPointNotFoundException)
                throw new PlatformNotSupportedException("Required native directory identity APIs are unavailable.", error);
            throw;
        }
    }

    internal ObservedRootRelation Inspect(RootRole first, RootRole second)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var left = roots[RoleIndex(first)];
            var right = roots[RoleIndex(second)];
            // These are real handle metadata reads, not path or comparer callbacks.
            foreach (var root in roots) root.RequireCapturedIdentities();
            if (left.Identity == right.Identity) return ObservedRootRelation.SameDirectory;
            if (right.AncestorsContain(left.Identity)) return ObservedRootRelation.FirstIsAncestor;
            if (left.AncestorsContain(right.Identity)) return ObservedRootRelation.SecondIsAncestor;
            return ObservedRootRelation.DisjointCapturedChains;
        }
    }

    private static int RoleIndex(RootRole role) => role switch
    {
        RootRole.Users => 0,
        RootRole.Items => 1,
        RootRole.Quests => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            for (var index = roots.Length - 1; index >= 0; index--) roots[index].Dispose();
        }
    }

    internal enum RootRole { Users, Items, Quests }
    internal enum ObservedRootRelation { SameDirectory, FirstIsAncestor, SecondIsAncestor, DisjointCapturedChains }
}
