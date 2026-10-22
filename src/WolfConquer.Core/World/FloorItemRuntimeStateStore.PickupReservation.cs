namespace WolfConquer.Core.World;

public sealed partial class FloorItemRuntimeStateStore
{
    private readonly Dictionary<uint, PickupReservation> _pendingPickupByUid = new();

    internal FloorItemRuntimePickupStatus TryReservePickup(FloorItemRuntimeEntry expected, uint playerUid,
        bool allowOwnerLockBypass, DateTimeOffset now, out PickupReservation? reservation)
    {
        ArgumentNullException.ThrowIfNull(expected);
        lock (_gate)
        {
            reservation = null;
            if (!_visibleByUid.TryGetValue(expected.Packet.Uid, out var state) ||
                !ReferenceEquals(state.Entry, expected)) return FloorItemRuntimePickupStatus.Missing;
            if (_pendingPickupByUid.ContainsKey(expected.Packet.Uid)) return FloorItemRuntimePickupStatus.Busy;
            if (IsExpiredNoLock(state, now))
            {
                RemoveNoLock(expected.Packet.Uid, out _);
                return FloorItemRuntimePickupStatus.Expired;
            }
            // Native floor effects are not pickable inventory/currency objects.
            if (expected.Effect is not null || expected.Packet.DropType != LegacyFloorItemDropType.Visible)
                return FloorItemRuntimePickupStatus.Missing;
            if (!allowOwnerLockBypass && IsOwnerLockedNoLock(state, playerUid, now))
                return FloorItemRuntimePickupStatus.OwnerLocked;
            // Every pending ground token also promises a future visible slot.
            // Do not admit pickup locks that could make its publication impossible.
            if (_pendingPickupByUid.Count + _pendingGroundByUid.Count >= _capacity)
                return FloorItemRuntimePickupStatus.Busy;
            reservation = new PickupReservation(this, expected);
            _pendingPickupByUid.Add(expected.Packet.Uid, reservation);
            return FloorItemRuntimePickupStatus.Available;
        }
    }

    private void ThrowIfPickupReservationCollisionNoLock(FloorItemRuntimeEntry entry)
    {
        if (_pendingPickupByUid.ContainsKey(entry.Packet.Uid))
            throw new InvalidOperationException("A pending pickup reservation owns the floor UID.");
    }

    internal sealed class PickupReservation : IDisposable
    {
        private readonly FloorItemRuntimeStateStore _store;

        internal PickupReservation(FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
        { _store = store; Entry = entry; }

        internal FloorItemRuntimeEntry Entry { get; }

        internal bool TryConsume(out FloorItemRuntimeEntry entry)
        {
            lock (_store._gate)
            {
                entry = null!;
                if (!_store._pendingPickupByUid.TryGetValue(Entry.Packet.Uid, out var current) ||
                    !ReferenceEquals(current, this) ||
                    !_store._visibleByUid.TryGetValue(Entry.Packet.Uid, out var state) ||
                    !ReferenceEquals(state.Entry, Entry)) return false;
                _store._pendingPickupByUid.Remove(Entry.Packet.Uid);
                // TTL and owner admission were checked at reservation. Rechecking
                // after a successful disk save could strand a committed pickup.
                return _store.RemoveNoLock(Entry.Packet.Uid, out entry);
            }
        }

        public void Dispose()
        {
            lock (_store._gate)
            {
                if (_store._pendingPickupByUid.TryGetValue(Entry.Packet.Uid, out var current) &&
                    ReferenceEquals(current, this)) _store._pendingPickupByUid.Remove(Entry.Packet.Uid);
            }
        }
    }
}
