namespace WolfConquer.Core.World;

internal enum LegacyJournalFault { Stop, TargetReadLease, TargetExclusiveLease, PendingReadLease,
    ThrowIo, ThrowSecurity, ThrowInvalidOperation, Observe }

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

    private LegacyJournalInterruption(LegacyJournalFault kind, int boundary, int ordinal = -1)
    {
        if (!Enum.IsDefined(kind) || boundary is < 0 or > 4 || ordinal is < -1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(boundary));
        this.kind = kind; this.boundary = boundary; this.ordinal = ordinal;
    }

    internal static LegacyJournalInterruption StopAfter(int boundary) => new(LegacyJournalFault.Stop, boundary);
    internal static LegacyJournalInterruption Observe() => new(LegacyJournalFault.Observe, 0);
    internal static LegacyJournalInterruption LeaseTargetAfter(int boundary, int ordinal, bool exclusive = false) =>
        new(exclusive ? LegacyJournalFault.TargetExclusiveLease : LegacyJournalFault.TargetReadLease, boundary, ordinal);
    internal static LegacyJournalInterruption LeasePendingAfter(int boundary) => new(LegacyJournalFault.PendingReadLease, boundary);
    internal static LegacyJournalInterruption ThrowAfter(int boundary, LegacyJournalFault fault)
    {
        if (fault is not (LegacyJournalFault.ThrowIo or LegacyJournalFault.ThrowSecurity or LegacyJournalFault.ThrowInvalidOperation))
            throw new ArgumentOutOfRangeException(nameof(fault));
        return new(fault, boundary);
    }

    internal int[] Visited { get { lock (gate) return visited.ToArray(); } }
    internal byte[]?[] BoundaryImages { get { lock (gate) return images.Select(image => image?.ToArray()).ToArray(); } }

    internal void Claim(int targetCount)
    {
        lock (gate)
        {
            if (disposed || consumed || targetCount is < 1 or > 4 || boundary > targetCount ||
                (kind is LegacyJournalFault.TargetReadLease or LegacyJournalFault.TargetExclusiveLease) &&
                    (ordinal < 0 || ordinal >= targetCount) ||
                kind == LegacyJournalFault.PendingReadLease && boundary != targetCount)
                throw new ArgumentException("Interruption boundary/target is invalid or already consumed.");
            consumed = true;
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
