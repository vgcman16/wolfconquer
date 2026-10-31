namespace WolfConquer.Core.World;

internal enum LegacyJournalFault { Stop, TargetReadLease, TargetExclusiveLease, PendingReadLease,
    ThrowIo, ThrowSecurity, ThrowInvalidOperation, Observe, StopAfterStageFlushed, ItemSpellTargetBeforeWriter }

// Closed failure-only control for the real engine. A caller supplies no path,
// delegate, saver or result; only the engine passes its validated target list.
internal sealed class LegacyJournalInterruption : IDisposable
{
    private const int MaxImageBytes = 16 * 1024 * 1024;
    private readonly object gate = new();
    private readonly LegacyJournalFault kind;
    private readonly int boundary, ordinal;
    private readonly List<int> visited = [];
    private byte[]?[] images = [];
    private FileStream? lease;
    private bool consumed, disposed;
    internal bool PendingObserved { get; private set; }
    internal bool ItemSpellTargetLeaseObserved { get; private set; }

    private LegacyJournalInterruption(LegacyJournalFault kind, int boundary, int ordinal = -1)
    {
        if (!Enum.IsDefined(kind) || boundary is < 0 or > 6 || ordinal is < -1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(boundary));
        this.kind = kind; this.boundary = boundary; this.ordinal = ordinal;
    }

    internal static LegacyJournalInterruption StopAfter(int boundary) => boundary is >= 0 and <= 4
        ? new(LegacyJournalFault.Stop, boundary) : throw new ArgumentOutOfRangeException(nameof(boundary));
    internal static LegacyJournalInterruption StopAfterAuthenticatedCreation(int boundary) => new(LegacyJournalFault.Stop, boundary);
    internal static LegacyJournalInterruption StopAfterAuthenticatedCreationStageFlushed(int ordinal)
    {
        if (ordinal is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new(LegacyJournalFault.StopAfterStageFlushed, ordinal);
    }
    internal static LegacyJournalInterruption StopAfterStageFlushed(int ordinal)
    {
        if (ordinal is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new(LegacyJournalFault.StopAfterStageFlushed, ordinal);
    }
    internal static LegacyJournalInterruption Observe() => new(LegacyJournalFault.Observe, 0);
    internal static LegacyJournalInterruption LeaseItemSpellTargetBeforeWriter(int ordinal)
    {
        if (ordinal is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new(LegacyJournalFault.ItemSpellTargetBeforeWriter, 0, ordinal);
    }
    internal static LegacyJournalInterruption LeaseTargetAfter(int boundary, int ordinal, bool exclusive = false) =>
        boundary is >= 0 and <= 4
            ? new(exclusive ? LegacyJournalFault.TargetExclusiveLease : LegacyJournalFault.TargetReadLease, boundary, ordinal)
            : throw new ArgumentOutOfRangeException(nameof(boundary));
    internal static LegacyJournalInterruption LeasePendingAfter(int boundary) => boundary is >= 0 and <= 4
        ? new(LegacyJournalFault.PendingReadLease, boundary) : throw new ArgumentOutOfRangeException(nameof(boundary));
    internal static LegacyJournalInterruption ThrowAfter(int boundary, LegacyJournalFault fault)
    {
        if (fault is not (LegacyJournalFault.ThrowIo or LegacyJournalFault.ThrowSecurity or LegacyJournalFault.ThrowInvalidOperation))
            throw new ArgumentOutOfRangeException(nameof(fault));
        if (boundary is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(boundary));
        return new(fault, boundary);
    }

    internal int[] Visited { get { lock (gate) return visited.ToArray(); } }
    internal byte[]?[] BoundaryImages { get { lock (gate) return images.Select(image => image?.ToArray()).ToArray(); } }

    // Only the actual Decompose issuer can derive the saved target. This holds
    // a real OS read lease, forcing the existing final saved-input proof to
    // encounter IO failure; it supplies no replacement path, bytes or writer.
    internal void ArmRuneDecomposeBeforeWriter(LegacyRuneDecomposeEvidence producer)
    {
        lock (gate)
        {
            if (kind != LegacyJournalFault.ItemSpellTargetBeforeWriter) return;
            ObjectDisposedException.ThrowIf(disposed, this);
            if (consumed) throw new InvalidOperationException("Pre-writer target lease is already consumed.");
            ArgumentNullException.ThrowIfNull(producer);
            string target = producer.SavedBeforePath(ordinal);
            lease = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);
            consumed = true; ItemSpellTargetLeaseObserved = true;
        }
    }

    internal void Claim(int targetCount, int journalVersion)
    {
        lock (gate)
        {
            if (disposed || consumed || kind == LegacyJournalFault.ItemSpellTargetBeforeWriter ||
                targetCount < 1 || targetCount > (journalVersion == 19 ? 6 : 4) || boundary > targetCount ||
                (kind is LegacyJournalFault.TargetReadLease or LegacyJournalFault.TargetExclusiveLease) &&
                    (ordinal < 0 || ordinal >= targetCount) ||
                kind == LegacyJournalFault.StopAfterStageFlushed && journalVersion is not (1 or 2 or 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19) ||
                kind == LegacyJournalFault.PendingReadLease && boundary != targetCount)
                throw new ArgumentException("Interruption boundary/target is invalid or already consumed.");
            consumed = true;
        }
    }

    // A genuine engine-written/closed/flush-complete stage is retained only by
    // this closed STOP control. It cannot supply bytes, paths or a writer.
    internal bool AtStageFlushed(int count, IReadOnlyList<string> targets, string pending)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!consumed) throw new InvalidOperationException("Interruption has no engine invocation.");
            if (kind != LegacyJournalFault.StopAfterStageFlushed || count != boundary) return false;
            visited.Add(count);
            PendingObserved = File.Exists(pending);
            images = targets.Select(ReadImage).ToArray();
            return true;
        }
    }

    // Called only by CommitLocked with its already-declared canonical targets.
    // Direct FileStream operations acquire no game/Store/snapshot monitor.
    internal bool AtBoundary(int count, IReadOnlyList<string> targets, string pending)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!consumed) throw new InvalidOperationException("Interruption has no engine invocation.");
            visited.Add(count);
            if (count != boundary) return false;
            PendingObserved = File.Exists(pending);
            images = targets.Select(ReadImage).ToArray();
            switch (kind)
            {
                case LegacyJournalFault.Stop: return true;
                case LegacyJournalFault.StopAfterStageFlushed: return false;
                case LegacyJournalFault.Observe: return false;
                case LegacyJournalFault.TargetReadLease:
                    lease = new FileStream(targets[ordinal], FileMode.Open, FileAccess.Read, FileShare.Read); break;
                case LegacyJournalFault.TargetExclusiveLease:
                    lease = new FileStream(targets[ordinal], FileMode.Open, FileAccess.ReadWrite, FileShare.None); break;
                case LegacyJournalFault.PendingReadLease:
                    lease = new FileStream(pending, FileMode.Open, FileAccess.Read, FileShare.Read); break;
                case LegacyJournalFault.ThrowIo: throw new IOException("Closed journal IO failure.");
                case LegacyJournalFault.ThrowSecurity: throw new System.Security.SecurityException("Closed journal security failure.");
                case LegacyJournalFault.ThrowInvalidOperation: throw new InvalidOperationException("Closed journal wrapper failure.");
                default: throw new ArgumentOutOfRangeException();
            }
            return false;
        }
    }

    private static byte[]? ReadImage(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxImageBytes) throw new IOException("Fault observation image is too large.");
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            lease?.Dispose(); lease = null; disposed = true;
        }
    }
}
