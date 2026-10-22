namespace WolfConquer.Core.World;

public sealed partial class FloorItemRuntimeStateStore
{
    // Native money generation follows AddGroundItem, not death/selection. The
    // factory can supply only money ID/amount; placement identity stays owned here.
    internal bool TryAddMoneyVisibleOnGround(
        FloorItemRuntimeEntry entry,
        DateTimeOffset now,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        Func<(uint ItemId, uint Amount)?> generateMoneyPayload,
        out FloorItemRuntimeEntry added)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(generateMoneyPayload);
        if (entry.Kind != FloorItemRuntimePickupKind.Money)
        {
            throw new ArgumentException("Only money payloads can be generated after ground placement.", nameof(entry));
        }

        return TryAddVisibleOnGroundCore(entry, now, range, isGroundValid, generateMoneyPayload, out added);
    }

    // The real allocator runs only after ground and payload admission. A drawn
    // UID may be zero after native uint wrap; it is not an undrawn draft anymore.
    internal bool TryAddMoneyVisibleOnGround(
        FloorItemRuntimeEntry entry,
        DateTimeOffset now,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        Func<(uint ItemId, uint Amount)?> generateMoneyPayload,
        Func<uint> allocateFloorUid,
        out FloorItemRuntimeEntry added)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(generateMoneyPayload);
        ArgumentNullException.ThrowIfNull(allocateFloorUid);
        if (entry.Kind != FloorItemRuntimePickupKind.Money)
        {
            throw new ArgumentException("Only money payloads can be generated after ground placement.", nameof(entry));
        }
        if (entry.Packet.Uid != 0)
        {
            throw new ArgumentException("A deferred floor UID requires an undrawn draft.", nameof(entry));
        }

        return TryAddVisibleOnGroundCore(entry, now, range, isGroundValid, generateMoneyPayload,
            out added, allocateFloorUid);
    }

    private bool TryAddVisibleOnGroundCore(
        FloorItemRuntimeEntry entry,
        DateTimeOffset now,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        Func<(uint ItemId, uint Amount)?>? generateMoneyPayload,
        out FloorItemRuntimeEntry added,
        Func<uint>? allocateFloorUid = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Packet.DropType != LegacyFloorItemDropType.Visible)
        {
            throw new ArgumentException("Only visible floor item packets can be added.", nameof(entry));
        }

        lock (_gate)
        {
            if (allocateFloorUid is null &&
                (_pendingGroundByUid.ContainsKey(entry.Packet.Uid) || _pendingPickupByUid.ContainsKey(entry.Packet.Uid)))
            {
                added = default!;
                return false;
            }

            RemoveExpiredNoLock(now);
            if (_visibleByUid.Count >= _capacity && _visibleByUid.Count == _pendingPickupByUid.Count)
            {
                added = default!;
                return false;
            }

            var x = entry.Packet.X;
            var y = entry.Packet.Y;
            if (!CanPlaceGroundItemNoLock(entry.MapId, x, y, isGroundValid) &&
                !TryFindGroundItemLocationNoLock(entry.MapId, x, y, range, isGroundValid, out x, out y))
            {
                added = default!;
                return false;
            }

            var placed = entry with { Packet = entry.Packet with { X = x, Y = y } };
            if (generateMoneyPayload is not null)
            {
                var payload = generateMoneyPayload();
                if (!payload.HasValue || payload.Value.ItemId == 0 || payload.Value.Amount == 0)
                {
                    added = default!;
                    return false;
                }

                var money = payload.Value;
                placed = placed with
                {
                    Packet = placed.Packet with { ItemId = money.ItemId },
                    Amount = money.Amount,
                };
            }

            if (allocateFloorUid is not null)
            {
                var uid = allocateFloorUid();
                // The native constructor consumes its UID before EnqueueItem.
                // Never retry the allocator or overwrite a current/reserved UID.
                if (_visibleByUid.ContainsKey(uid) || _pendingGroundByUid.ContainsKey(uid) ||
                    _pendingPickupByUid.ContainsKey(uid))
                {
                    added = default!;
                    return false;
                }

                placed = placed with { Packet = placed.Packet with { Uid = uid } };
            }

            added = placed;
            AddVisibleNoLock(added, now);
            return true;
        }
    }
}
