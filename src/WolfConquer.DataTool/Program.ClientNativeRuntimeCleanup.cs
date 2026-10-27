using System.ComponentModel;
using System.Diagnostics;

internal static partial class Program
{
    internal static bool IsClientNativeCleanupCreationWithinLifetime(DateTime childBorn, DateTime parentBorn,
        DateTime? parentExited, DateTime snapshotBoundary)
    {
        if (childBorn.Kind != DateTimeKind.Utc || parentBorn.Kind != DateTimeKind.Utc ||
            snapshotBoundary.Kind != DateTimeKind.Utc || snapshotBoundary < parentBorn) return false;
        if (parentExited is DateTime ended && (ended.Kind != DateTimeKind.Utc || ended < parentBorn)) return false;
        return childBorn >= parentBorn && childBorn <= snapshotBoundary &&
            (parentExited is null || childBorn <= parentExited.Value);
    }

    // A current OS parent snapshot plus retained process handles is required.
    // An old PID/name/path observation is never a cleanup grant.
    private sealed class ClientNativeCleanupCapture : IDisposable
    {
        private readonly List<Process> processes = [];
        private readonly HashSet<int> processIds = [];
        private readonly List<string> errors = [];
        private readonly Process root;
        internal IReadOnlyList<Process> Processes => processes;
        internal IReadOnlySet<int> ProcessIds => processIds;
        internal IReadOnlyList<string> Errors => errors;

        private ClientNativeCleanupCapture(Process root)
        {
            this.root = root;
            _ = root.SafeHandle;
            _ = root.StartTime;
            processes.Add(root);
            processIds.Add(root.Id);
        }

        internal static ClientNativeCleanupCapture Prepare(Process root)
        {
            var capture = new ClientNativeCleanupCapture(root);
            try { capture.ObserveCurrentDescendants(); return capture; }
            catch { capture.Dispose(); throw; }
        }

        private void ObserveCurrentDescendants()
        {
            // A PID reused after this snapshot starts cannot pass the creation
            // boundary after its actual kernel handle is opened.
            var snapshotBoundary = DateTime.UtcNow;
            var entries = EnumerateClientNativeRuntimeProcessEntries();
            var children = entries.GroupBy(entry => entry.ParentProcessId)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.ProcessId).ToArray());
            var queue = new Queue<Process>();
            queue.Enqueue(root);
            while (queue.TryDequeue(out var parent))
            {
                if (!children.TryGetValue(parent.Id, out var candidates)) continue;
                foreach (var id in candidates)
                {
                    if (id <= 0 || processIds.Contains(id)) continue;
                    var child = TryCaptureChild(parent, id, snapshotBoundary);
                    if (child is null) continue;
                    processes.Add(child);
                    processIds.Add(id);
                    queue.Enqueue(child);
                }
            }
        }

        private Process? TryCaptureChild(Process parent, int id, DateTime snapshotBoundary)
        {
            Process? child = null;
            try
            {
                child = Process.GetProcessById(id);
                _ = child.SafeHandle;
                var born = child.StartTime.ToUniversalTime();
                var parentBorn = parent.StartTime.ToUniversalTime();
                DateTime? parentExited = parent.HasExited ? parent.ExitTime.ToUniversalTime() : null;
                // The Toolhelp parent ID is meaningful only within the exact
                // retained parent's lifetime. A reused parent PID's later child
                // cannot be adopted after the original parent has exited.
                if (!IsClientNativeCleanupCreationWithinLifetime(born, parentBorn, parentExited, snapshotBoundary))
                {
                    errors.Add($"Current descendant attribution refused pid={id}.");
                    child.Dispose();
                    return null;
                }
                return child;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
            {
                child?.Dispose();
                errors.Add($"Current descendant capture unavailable pid={id}: {ex.GetType().Name}.");
                return null;
            }
        }

        public void Dispose()
        {
            foreach (var child in processes)
                if (!ReferenceEquals(child, root)) child.Dispose();
        }
    }
}
