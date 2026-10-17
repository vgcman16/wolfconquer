using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum FloorItemRuntimePickupKind
{
    Item,
    Money,
    ConquerPoints,
}

public enum FloorItemRuntimePickupStatus
{
    Available,
    Missing,
    OwnerLocked,
    Expired,
}

public sealed record FloorItemRuntimeEntry(
    long MapId,
    LegacyFloorItemPacket Packet,
    FloorItemRuntimePickupKind Kind,
    long Amount,
    PlayerItemSnapshotItem? Item,
    bool OwnerOnly,
    FloorItemRuntimeEffect? Effect = null)
{
    public static FloorItemRuntimeEntry ForItem(
        long mapId,
        LegacyFloorItemPacket packet,
        PlayerItemSnapshotItem? item = null,
        bool ownerOnly = false)
    {
        return new FloorItemRuntimeEntry(mapId, packet, FloorItemRuntimePickupKind.Item, Amount: 0, item, ownerOnly);
    }

    public static FloorItemRuntimeEntry ForMoney(
        long mapId,
        LegacyFloorItemPacket packet,
        long amount,
        bool ownerOnly = false)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Floor item money amount must be positive.");
        }

        return new FloorItemRuntimeEntry(mapId, packet, FloorItemRuntimePickupKind.Money, amount, Item: null, ownerOnly);
    }

    public static FloorItemRuntimeEntry ForConquerPoints(
        long mapId,
        LegacyFloorItemPacket packet,
        long amount,
        bool ownerOnly = false)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Floor item Conquer Point amount must be positive.");
        }

        return new FloorItemRuntimeEntry(mapId, packet, FloorItemRuntimePickupKind.ConquerPoints, amount, Item: null, ownerOnly);
    }

    public static FloorItemRuntimeEntry ForEffect(
        long mapId,
        LegacyFloorItemPacket packet,
        FloorItemRuntimeEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return new FloorItemRuntimeEntry(
            mapId,
            packet,
            FloorItemRuntimePickupKind.Item,
            Amount: 0,
            Item: null,
            OwnerOnly: false,
            Effect: effect);
    }
}

public sealed record FloorItemRuntimeEffect(
    ushort SpellId,
    ushort OriginX,
    ushort OriginY,
    DateTimeOffset DueAt,
    int Distance = LegacyMoveLineGeometry.LegacyDistance,
    double HitRadius = 2.0,
    int PlayerDamagePercent = 100,
    int PlayerAttackBonus = 0,
    uint RemovalActionType = 0,
    int RemovalVisibilityRadius = 18,
    DateTimeOffset? ExpiresAt = null,
    int RepeatMilliseconds = 0,
    byte SpellLevel = 0,
    byte LevelHu = 0,
    uint RuntimeOwnerUid = 0,
    DateTimeOffset? NextRepeatAt = null);

public sealed record FloorItemRuntimeDueEffects(
    IReadOnlyList<FloorItemRuntimeEntry> Invoked,
    IReadOnlyList<FloorItemRuntimeEntry> Expired);

public sealed class FloorItemRuntimeStateStore
{
    public const int DefaultCapacity = 4_096;
    public static readonly TimeSpan LegacyOwnerOnlyDuration = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan LegacyLifetime = TimeSpan.FromSeconds(40);

    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<uint, FloorItemRuntimeState> _visibleByUid = new();
    private readonly LinkedList<uint> _insertionOrder = new();

    public FloorItemRuntimeStateStore(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Floor item runtime state capacity must be positive.");
        }

        _capacity = capacity;
    }

    public void AddVisiblePacket(LegacyFloorItemPacket packet)
    {
        AddVisible(FloorItemRuntimeEntry.ForItem(mapId: 0, packet));
    }

    public void AddVisible(FloorItemRuntimeEntry entry)
    {
        AddVisible(entry, DateTimeOffset.UtcNow);
    }

    public void AddVisible(FloorItemRuntimeEntry entry, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!CanStoreVisibleRuntimePacket(entry.Packet.DropType))
        {
            throw new ArgumentException("Only visible floor item or effect packets can be added.", nameof(entry));
        }

        lock (_gate)
        {
            RemoveExpiredNoLock(now);
            AddVisibleNoLock(entry, now);
        }
    }

    public bool TryGetVisiblePacket(uint floorUid, out LegacyFloorItemPacket packet)
    {
        return TryGetVisiblePacket(floorUid, DateTimeOffset.UtcNow, out packet);
    }

    public bool TryGetVisiblePacket(
        uint floorUid,
        DateTimeOffset now,
        out LegacyFloorItemPacket packet)
    {
        if (TryGetVisibleEntry(floorUid, now, out var entry))
        {
            packet = entry.Packet;
            return true;
        }

        packet = default!;
        return false;
    }

    public bool TryGetVisibleEntry(uint floorUid, out FloorItemRuntimeEntry entry)
    {
        return TryGetVisibleEntry(floorUid, DateTimeOffset.UtcNow, out entry);
    }

    public bool TryGetVisibleEntry(
        uint floorUid,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        lock (_gate)
        {
            if (!_visibleByUid.TryGetValue(floorUid, out var state))
            {
                entry = default!;
                return false;
            }

            if (IsExpiredNoLock(state, now))
            {
                RemoveNoLock(floorUid, out _);
                entry = default!;
                return false;
            }

            entry = state.Entry;
            return true;
        }
    }

    public bool TryConsumePickup(uint floorUid, out LegacyFloorItemPacket packet)
    {
        if (TryConsumePickupEntry(floorUid, out var entry))
        {
            packet = entry.Packet;
            return true;
        }

        packet = default!;
        return false;
    }

    public bool TryConsumePickupEntry(uint floorUid, out FloorItemRuntimeEntry entry)
    {
        return TryConsumePickupEntry(
            floorUid,
            playerUid: null,
            allowOwnerLockBypass: false,
            DateTimeOffset.UtcNow,
            out entry) == FloorItemRuntimePickupStatus.Available;
    }

    public FloorItemRuntimePickupStatus TryConsumePickupEntry(
        uint floorUid,
        uint playerUid,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        return TryConsumePickupEntry(
            floorUid,
            (uint?)playerUid,
            allowOwnerLockBypass: false,
            now,
            out entry);
    }

    public FloorItemRuntimePickupStatus TryConsumePickupEntry(
        uint floorUid,
        uint playerUid,
        bool allowOwnerLockBypass,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        return TryConsumePickupEntry(
            floorUid,
            (uint?)playerUid,
            allowOwnerLockBypass,
            now,
            out entry);
    }

    public FloorItemRuntimePickupStatus GetPickupStatus(
        uint floorUid,
        uint playerUid,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        lock (_gate)
        {
            return GetPickupStatusNoLock(floorUid, playerUid, now, out entry);
        }
    }

    public IReadOnlyList<FloorItemRuntimeEntry> ConsumeExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            return RemoveExpiredNoLock(now);
        }
    }

    public bool TryRemoveVisible(uint floorUid, out FloorItemRuntimeEntry entry)
    {
        lock (_gate)
        {
            return RemoveNoLock(floorUid, out entry);
        }
    }

    public IReadOnlyList<FloorItemRuntimeEntry> ConsumeDueEffects(ushort spellId, DateTimeOffset now)
    {
        lock (_gate)
        {
            List<uint>? dueUids = null;
            foreach (var pair in _visibleByUid)
            {
                if (pair.Value.Entry.Effect is not { } effect ||
                    effect.SpellId != spellId ||
                    now < effect.DueAt)
                {
                    continue;
                }

                dueUids ??= new List<uint>();
                dueUids.Add(pair.Key);
            }

            if (dueUids is null)
            {
                return [];
            }

            var due = new List<FloorItemRuntimeEntry>(dueUids.Count);
            foreach (var uid in dueUids)
            {
                if (RemoveNoLock(uid, out var entry))
                {
                    due.Add(entry);
                }
            }

            return due;
        }
    }

    public IReadOnlyList<FloorItemRuntimeEntry> ConsumeDueRepeatEffects(ushort spellId, DateTimeOffset now)
    {
        lock (_gate)
        {
            List<uint>? repeatUids = null;
            foreach (var pair in _visibleByUid)
            {
                if (pair.Value.Entry.Effect is not { } effect ||
                    effect.SpellId != spellId ||
                    effect.RepeatMilliseconds <= 0 ||
                    effect.NextRepeatAt is not { } nextRepeatAt ||
                    now <= nextRepeatAt ||
                    now >= effect.DueAt)
                {
                    continue;
                }

                repeatUids ??= new List<uint>();
                repeatUids.Add(pair.Key);
            }

            if (repeatUids is null)
            {
                return [];
            }

            var repeats = new List<FloorItemRuntimeEntry>(repeatUids.Count);
            foreach (var uid in repeatUids)
            {
                if (!_visibleByUid.TryGetValue(uid, out var state) ||
                    state.Entry.Effect is not { } effect)
                {
                    continue;
                }

                var updated = state.Entry with
                {
                    Effect = effect with { NextRepeatAt = now.AddMilliseconds(effect.RepeatMilliseconds) },
                };
                state.Entry = updated;
                repeats.Add(updated);
            }

            return repeats;
        }
    }

    public FloorItemRuntimeDueEffects ConsumeDueRepeatingEffects(ushort spellId, DateTimeOffset now)
    {
        lock (_gate)
        {
            List<uint>? invokedUids = null;
            List<uint>? expiredUids = null;
            foreach (var pair in _visibleByUid)
            {
                if (pair.Value.Entry.Effect is not { } effect ||
                    effect.SpellId != spellId)
                {
                    continue;
                }

                if (effect.ExpiresAt is { } expiresAt && now >= expiresAt)
                {
                    expiredUids ??= new List<uint>();
                    expiredUids.Add(pair.Key);
                    continue;
                }

                if (now < effect.DueAt)
                {
                    continue;
                }

                invokedUids ??= new List<uint>();
                invokedUids.Add(pair.Key);
            }

            var expired = new List<FloorItemRuntimeEntry>(expiredUids?.Count ?? 0);
            if (expiredUids is not null)
            {
                foreach (var uid in expiredUids)
                {
                    if (RemoveNoLock(uid, out var entry))
                    {
                        expired.Add(entry);
                    }
                }
            }

            var invoked = new List<FloorItemRuntimeEntry>(invokedUids?.Count ?? 0);
            if (invokedUids is not null)
            {
                foreach (var uid in invokedUids)
                {
                    if (!_visibleByUid.TryGetValue(uid, out var state) ||
                        state.Entry.Effect is not { } effect)
                    {
                        continue;
                    }

                    invoked.Add(state.Entry);
                    var nextDue = effect.RepeatMilliseconds > 0
                        ? now.AddMilliseconds(effect.RepeatMilliseconds)
                        : DateTimeOffset.MaxValue;
                    state.Entry = state.Entry with
                    {
                        Effect = effect with { DueAt = nextDue },
                    };
                }
            }

            return new FloorItemRuntimeDueEffects(invoked, expired);
        }
    }

    public IReadOnlyList<FloorItemRuntimeEntry> SnapshotVisible(DateTimeOffset now)
    {
        lock (_gate)
        {
            RemoveExpiredNoLock(now);
            if (_visibleByUid.Count == 0)
            {
                return [];
            }

            var entries = new List<FloorItemRuntimeEntry>(_visibleByUid.Count);
            foreach (var state in _visibleByUid.Values)
            {
                entries.Add(state.Entry);
            }

            return entries;
        }
    }

    public bool TryAddVisibleOnGround(
        FloorItemRuntimeEntry entry,
        byte range,
        out FloorItemRuntimeEntry added)
    {
        return TryAddVisibleOnGround(
            entry,
            DateTimeOffset.UtcNow,
            range,
            isGroundValid: null,
            out added);
    }

    public bool TryAddVisibleOnGround(
        FloorItemRuntimeEntry entry,
        DateTimeOffset now,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        out FloorItemRuntimeEntry added)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Packet.DropType != LegacyFloorItemDropType.Visible)
        {
            throw new ArgumentException("Only visible floor item packets can be added.", nameof(entry));
        }

        lock (_gate)
        {
            RemoveExpiredNoLock(now);

            var x = entry.Packet.X;
            var y = entry.Packet.Y;
            if (!CanPlaceGroundItemNoLock(entry.MapId, x, y, isGroundValid) &&
                !TryFindGroundItemLocationNoLock(entry.MapId, x, y, range, isGroundValid, out x, out y))
            {
                added = default!;
                return false;
            }

            added = entry with
            {
                Packet = entry.Packet with { X = x, Y = y },
            };
            AddVisibleNoLock(added, now);
            return true;
        }
    }

    private FloorItemRuntimePickupStatus TryConsumePickupEntry(
        uint floorUid,
        uint? playerUid,
        bool allowOwnerLockBypass,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        lock (_gate)
        {
            var status = playerUid is { } uid
                ? GetPickupStatusNoLock(floorUid, uid, now, out entry)
                : GetAnyPickupStatusNoLock(floorUid, now, out entry);
            if (status == FloorItemRuntimePickupStatus.OwnerLocked && allowOwnerLockBypass)
            {
                status = FloorItemRuntimePickupStatus.Available;
            }

            if (status != FloorItemRuntimePickupStatus.Available)
            {
                return status;
            }

            RemoveNoLock(floorUid, out entry);
            return FloorItemRuntimePickupStatus.Available;
        }
    }

    private void AddVisibleNoLock(FloorItemRuntimeEntry entry, DateTimeOffset now)
    {
        if (_visibleByUid.TryGetValue(entry.Packet.Uid, out var existing))
        {
            existing.Entry = entry;
            existing.CreatedAt = now;
            _insertionOrder.Remove(existing.OrderNode);
            _insertionOrder.AddLast(existing.OrderNode);
            return;
        }

        if (_visibleByUid.Count >= _capacity)
        {
            EvictOldestNoLock();
        }

        var orderNode = _insertionOrder.AddLast(entry.Packet.Uid);
        _visibleByUid.Add(entry.Packet.Uid, new FloorItemRuntimeState(entry, orderNode, now));
    }

    private static bool CanStoreVisibleRuntimePacket(LegacyFloorItemDropType dropType)
    {
        return dropType is LegacyFloorItemDropType.Visible or LegacyFloorItemDropType.Effect;
    }

    private FloorItemRuntimePickupStatus GetPickupStatusNoLock(
        uint floorUid,
        uint playerUid,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        var status = GetAnyPickupStatusNoLock(floorUid, now, out entry);
        if (status != FloorItemRuntimePickupStatus.Available)
        {
            return status;
        }

        var state = _visibleByUid[floorUid];
        return IsOwnerLockedNoLock(state, playerUid, now)
            ? FloorItemRuntimePickupStatus.OwnerLocked
            : FloorItemRuntimePickupStatus.Available;
    }

    private FloorItemRuntimePickupStatus GetAnyPickupStatusNoLock(
        uint floorUid,
        DateTimeOffset now,
        out FloorItemRuntimeEntry entry)
    {
        if (!_visibleByUid.TryGetValue(floorUid, out var state))
        {
            entry = default!;
            return FloorItemRuntimePickupStatus.Missing;
        }

        entry = state.Entry;
        if (!IsExpiredNoLock(state, now))
        {
            return FloorItemRuntimePickupStatus.Available;
        }

        RemoveNoLock(floorUid, out _);
        return FloorItemRuntimePickupStatus.Expired;
    }

    private bool TryFindGroundItemLocationNoLock(
        long mapId,
        ushort originX,
        ushort originY,
        byte range,
        Func<long, ushort, ushort, bool>? isGroundValid,
        out ushort x,
        out ushort y)
    {
        var radius = 1 + range;
        var minX = Math.Max(0, originX - radius);
        var minY = Math.Max(0, originY - radius);
        var maxX = Math.Min(ushort.MaxValue, originX + radius);
        var maxY = Math.Min(ushort.MaxValue, originY + radius);

        for (var candidateY = minY; candidateY <= maxY; candidateY++)
        {
            for (var candidateX = minX; candidateX <= maxX; candidateX++)
            {
                var checkedX = (ushort)candidateX;
                var checkedY = (ushort)candidateY;
                if (CanPlaceGroundItemNoLock(mapId, checkedX, checkedY, isGroundValid))
                {
                    x = checkedX;
                    y = checkedY;
                    return true;
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private bool CanPlaceGroundItemNoLock(
        long mapId,
        ushort x,
        ushort y,
        Func<long, ushort, ushort, bool>? isGroundValid)
    {
        if (IsGroundItemOccupiedNoLock(mapId, x, y))
        {
            return false;
        }

        return isGroundValid?.Invoke(mapId, x, y) ?? true;
    }

    private bool IsGroundItemOccupiedNoLock(long mapId, ushort x, ushort y)
    {
        foreach (var state in _visibleByUid.Values)
        {
            if (state.Entry.MapId == mapId &&
                state.Entry.Packet.X == x &&
                state.Entry.Packet.Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOwnerLockedNoLock(
        FloorItemRuntimeState state,
        uint playerUid,
        DateTimeOffset now)
    {
        return state.Entry.OwnerOnly &&
            state.Entry.Packet.ItemOwnerUid != 0 &&
            state.Entry.Packet.ItemOwnerUid != playerUid &&
            now <= state.CreatedAt + LegacyOwnerOnlyDuration;
    }

    private static bool IsExpiredNoLock(FloorItemRuntimeState state, DateTimeOffset now)
    {
        if (state.Entry.Effect?.ExpiresAt is not null)
        {
            return false;
        }

        return now >= state.CreatedAt + LegacyLifetime;
    }

    private IReadOnlyList<FloorItemRuntimeEntry> RemoveExpiredNoLock(DateTimeOffset now)
    {
        List<uint>? expiredUids = null;
        foreach (var pair in _visibleByUid)
        {
            if (!IsExpiredNoLock(pair.Value, now))
            {
                continue;
            }

            expiredUids ??= new List<uint>();
            expiredUids.Add(pair.Key);
        }

        if (expiredUids is null)
        {
            return [];
        }

        var expired = new List<FloorItemRuntimeEntry>(expiredUids.Count);
        foreach (var uid in expiredUids)
        {
            if (RemoveNoLock(uid, out var entry))
            {
                expired.Add(entry);
            }
        }

        return expired;
    }

    private bool RemoveNoLock(uint floorUid, out FloorItemRuntimeEntry entry)
    {
        if (!_visibleByUid.TryGetValue(floorUid, out var state))
        {
            entry = default!;
            return false;
        }

        _visibleByUid.Remove(floorUid);
        _insertionOrder.Remove(state.OrderNode);
        entry = state.Entry;
        return true;
    }

    private void EvictOldestNoLock()
    {
        var oldest = _insertionOrder.First;
        if (oldest is null)
        {
            return;
        }

        _visibleByUid.Remove(oldest.Value);
        _insertionOrder.RemoveFirst();
    }

    private sealed class FloorItemRuntimeState(
        FloorItemRuntimeEntry entry,
        LinkedListNode<uint> orderNode,
        DateTimeOffset createdAt)
    {
        public FloorItemRuntimeEntry Entry { get; set; } = entry;

        public LinkedListNode<uint> OrderNode { get; } = orderNode;

        public DateTimeOffset CreatedAt { get; set; } = createdAt;
    }
}
