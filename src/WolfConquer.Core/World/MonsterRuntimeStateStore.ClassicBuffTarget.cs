using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    internal bool TryCaptureClassicBuffTarget(LegacyMonsterInstance instance,
        long actorMap, ulong actorDynamic, long actorX, long actorY, long view,
        out ClassicBuffTarget? target, out string reason)
    {
        target = null; reason = "classicTargetBusy";
        // Movement callbacks can already own this monitor. Refuse their nested
        // admission before it acquires any Store/actor gate.
        if (Monitor.IsEntered(_gate)) return false;
        lock (_gate)
        {
            var state = GetOrCreateNoLock(instance);
            var capture = new ClassicBuffTarget(this, instance, state, state.ClassicBuffIncarnation,
                actorMap, actorDynamic, actorX, actorY, view);
            if (!capture.IsCurrentPlain()) { reason = "classicTargetUnavailable"; return false; }
            target = capture; reason = string.Empty; return true;
        }
    }

    internal sealed class ClassicBuffTarget
    {
        private readonly MonsterRuntimeStateStore owner;
        private readonly object state, incarnation;
        private readonly long actorMap, actorX, actorY, view;
        private readonly ulong actorDynamic;
        internal LegacyMonsterInstance Instance { get; }

        internal ClassicBuffTarget(MonsterRuntimeStateStore owner, LegacyMonsterInstance instance,
            object state, object incarnation, long actorMap, ulong actorDynamic, long actorX, long actorY, long view)
        {
            this.owner = owner; Instance = instance; this.state = state; this.incarnation = incarnation;
            this.actorMap = actorMap; this.actorDynamic = actorDynamic;
            this.actorX = actorX; this.actorY = actorY; this.view = view;
        }

        internal bool TryEnter(out ClassicBuffLease? lease, out string reason)
        {
            lease = null; reason = "classicTargetBusy";
            if (Monitor.IsEntered(owner._gate) || !Monitor.TryEnter(owner._gate)) return false;
            try
            {
                if (!IsCurrentPlain())
                { Monitor.Exit(owner._gate); reason = "classicTargetChanged"; return false; }
                lease = new(this, owner._gate); reason = string.Empty; return true;
            }
            catch { Monitor.Exit(owner._gate); throw; }
        }

        internal bool IsCurrentPlain()
        {
            if (!owner._states.TryGetValue(MonsterRuntimeKey.From(Instance), out var current) ||
                !ReferenceEquals(current, state) || !ReferenceEquals(current.ClassicBuffIncarnation, incarnation) ||
                current.DeadAt is not null || current.Hitpoints == 0 || current.RemovedFromMapOnDeath ||
                actorMap != Instance.MapId || actorDynamic != Instance.DynamicId) return false;
            // Decimal subtraction retains the source Chebyshev predicate without
            // overflowing on malformed signed actor coordinates.
            return Math.Max(Math.Abs((decimal)current.X - actorX), Math.Abs((decimal)current.Y - actorY)) <= view;
        }

        internal void ApplyHeld(int flag, TimeSpan duration, DateTimeOffset now, bool refresh,
            out LegacyMonsterTimedStatusApplyResult result, out LegacyMonsterInstance visible,
            out ulong[] vector)
        {
            if (!Monitor.IsEntered(owner._gate) || !IsCurrentPlain())
                throw new InvalidOperationException("classicMonsterLeaseNotCurrent");
            var current = (MonsterRuntimeState)state;
            visible = Instance with { X = current.X, Y = current.Y, Hitpoints = current.Hitpoints };
            result = owner.ApplyTimedStatusFlag(Instance, flag, duration, now, refresh);
            vector = owner.CreateStatusFlagVector(Instance, now, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        }
    }

    internal sealed class ClassicBuffLease(ClassicBuffTarget target, object gate) : IDisposable
    {
        private bool disposed;
        internal bool IsCurrentPlain() => !disposed && target.IsCurrentPlain();
        public void Dispose() { if (disposed) return; disposed = true; Monitor.Exit(gate); }
    }
}
