namespace WolfConquer.Core.World;

public sealed partial class FloorItemRuntimeStateStore
{
    // Pending entries are deliberately absent from the visible indexes consumed
    // by pickup, expiry, snapshots, and effects. No monitor is held by the token.
    private readonly Dictionary<uint, GroundReservation> _pendingGroundByUid = new();

    internal bool TryReserveGround(
        FloorItemRuntimeEntry entry,
        DateTimeOffset now,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        out GroundReservation? reservation)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Packet.DropType != LegacyFloorItemDropType.Visible)
        {
            throw new ArgumentException("Only visible floor item packets can be reserved.", nameof(entry));
        }

        lock (_gate)
        {
            reservation = null;
            RemoveExpiredNoLock(now);
            if (_pendingGroundByUid.Count + _pendingPickupByUid.Count >= _capacity ||
                _pendingGroundByUid.ContainsKey(entry.Packet.Uid) ||
                _visibleByUid.ContainsKey(entry.Packet.Uid))
            {
                return false;
            }

            var x = entry.Packet.X;
            var y = entry.Packet.Y;
            if (!CanPlaceGroundItemNoLock(entry.MapId, x, y, isGroundValid) &&
                !TryFindGroundItemLocationNoLock(entry.MapId, x, y, range, isGroundValid, out x, out y))
            {
                return false;
            }

            var placed = entry with { Packet = entry.Packet with { X = x, Y = y } };
            reservation = new GroundReservation(this, placed);
            _pendingGroundByUid.Add(placed.Packet.Uid, reservation);
            return true;
        }
    }

    private bool IsGroundItemReservedNoLock(long mapId, ushort x, ushort y)
    {
        foreach (var pending in _pendingGroundByUid.Values)
        {
            if (pending.Entry.MapId == mapId && pending.Entry.Packet.X == x && pending.Entry.Packet.Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private void ThrowIfGroundReservationCollisionNoLock(FloorItemRuntimeEntry entry)
    {
        if (_pendingGroundByUid.ContainsKey(entry.Packet.Uid) ||
            IsGroundItemReservedNoLock(entry.MapId, entry.Packet.X, entry.Packet.Y))
        {
            throw new InvalidOperationException("A pending ground reservation owns the floor UID or tile.");
        }
    }

    internal sealed class GroundReservation : IDisposable
    {
        private readonly FloorItemRuntimeStateStore _store;

        internal GroundReservation(FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
        {
            _store = store;
            Entry = entry;
        }

        internal FloorItemRuntimeEntry Entry { get; }

        internal bool TryPublish(DateTimeOffset now)
        {
            lock (_store._gate)
            {
                if (!_store._pendingGroundByUid.TryGetValue(Entry.Packet.Uid, out var current) ||
                    !ReferenceEquals(current, this))
                {
                    return false;
                }

                _store._pendingGroundByUid.Remove(Entry.Packet.Uid);
                _store.RemoveExpiredNoLock(now);
                // All intervening public adds exclude this UID and tile. Capacity
                // eviction is deferred until here, never performed by reservation.
                _store.AddVisibleNoLock(Entry, now);
                return true;
            }
        }

        public void Dispose()
        {
            lock (_store._gate)
            {
                if (_store._pendingGroundByUid.TryGetValue(Entry.Packet.Uid, out var current) &&
                    ReferenceEquals(current, this))
                {
                    _store._pendingGroundByUid.Remove(Entry.Packet.Uid);
                }
            }
        }
    }
}
