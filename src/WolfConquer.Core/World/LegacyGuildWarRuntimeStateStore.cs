using System.Threading;

namespace WolfConquer.Core.World;

public sealed class LegacyGuildWarRuntimeStateStore
{
    private int _isActive;
    private readonly object permissionGate = new();
    private object permissionGeneration = new();
    private long preparedAdmissions;
    internal long PreparedAdmissionCount => Interlocked.Read(ref preparedAdmissions);

    public bool IsActive => Volatile.Read(ref _isActive) != 0;

    public void SetActive(bool active)
    {
        if (Monitor.IsEntered(permissionGate)) throw new InvalidOperationException("guildWarPermissionReentry");
        var next = new object();
        lock (permissionGate)
        {
            Volatile.Write(ref _isActive, active ? 1 : 0);
            permissionGeneration = next;
        }
    }

    // Captured only from this actual manager. Every real producer rotates the
    // generation, including equal-value assignments, so an ABA cannot revive it.
    internal bool TryCapturePermission(out PermissionObservation? observation)
    {
        observation = null;
        if (Monitor.IsEntered(permissionGate) || !Monitor.TryEnter(permissionGate)) return false;
        try
        {
            observation = new(this, permissionGeneration, _isActive != 0);
            return true;
        }
        finally { Monitor.Exit(permissionGate); }
    }

    internal sealed class PermissionObservation(LegacyGuildWarRuntimeStateStore owner, object generation, bool active)
    {
        internal bool TryEnter(out PermissionLease? lease)
        {
            lease = null;
            if (Monitor.IsEntered(owner.permissionGate) || !Monitor.TryEnter(owner.permissionGate)) return false;
            try
            {
                if (!CurrentHeld()) { Monitor.Exit(owner.permissionGate); return false; }
                lease = new(this, owner.permissionGate);
                return true;
            }
            catch { Monitor.Exit(owner.permissionGate); throw; }
        }

        internal void RecordPreparedHeld()
        {
            if (!CurrentHeld()) throw new InvalidOperationException("guildWarPermissionPreparationChanged");
            Interlocked.Increment(ref owner.preparedAdmissions);
        }
        internal bool CurrentHeld() => Monitor.IsEntered(owner.permissionGate) &&
            ReferenceEquals(owner.permissionGeneration, generation) && (owner._isActive != 0) == active;
    }

    internal sealed class PermissionLease(PermissionObservation observation, object gate) : IDisposable
    {
        private bool disposed;
        internal bool IsCurrentPlain() => !disposed && observation.CurrentHeld();
        public void Dispose()
        {
            if (disposed) return;
            if (!Monitor.IsEntered(gate))
                throw new SynchronizationLockException("Guild permission lease must be released by its owning thread.");
            disposed = true;
            Monitor.Exit(gate);
        }
    }
    public void Start()
    {
        SetActive(true);
    }

    public void Stop()
    {
        SetActive(false);
    }

    public bool CanBroadcastMsgNewSynWarOwnershipChanged(uint syndicateId)
    {
        return IsMsgNewSynWarBroadcastAllowed(syndicateId);
    }

    public bool CanBroadcastMsgNewSynWarDestroyed(uint syndicateId)
    {
        return IsMsgNewSynWarBroadcastAllowed(syndicateId);
    }

    private bool IsMsgNewSynWarBroadcastAllowed(uint syndicateId)
    {
        return IsActive && syndicateId != 0;
    }
}
