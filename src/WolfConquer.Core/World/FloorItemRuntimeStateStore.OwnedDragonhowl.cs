using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class FloorItemRuntimeStateStore
{
    internal bool TryPrepareDragonhowlGround(WorldSessionStore.OwnedDragonhowlGroundAttempt purpose,
        out PreparedDragonhowlGround? prepared, out string reason)
    {
        prepared = null; reason = "ownedDragonhowlGroundOwnerUnavailable";
        if (!purpose.TryReadPreparation(this, out var data, out var query, out var now, out var previous)) return false;
        var terrain = new DragonhowlGroundTerrain(query, data.Recipe.Monster.MapId);
        lock (_gate)
        {
            // Local DATA sequence only: no UID, live reservation, expiry removal
            // or capacity eviction occurs while the whole proc is prepared.
            var sequence = previous?.sequence ?? new DragonhowlGroundSequence(this);
            if (previous is not null && (!ReferenceEquals(previous.store, this) ||
                !previous.InputsAreCurrentPlain()) || !sequence.IsCurrent(this)) return false;
            var choice = ChooseDragonhowlGround(data, terrain, sequence.Projection, sequence.Expected, now);
            if (!data.TryPreparePayload(choice.Placed) || !terrain.IsCurrentPlain() || !sequence.IsCurrent(this))
            { reason = "ownedDragonhowlGroundPayloadUnavailable"; return false; }
            var result = new PreparedDragonhowlGround(this, purpose, data, terrain, sequence, choice, now);
            sequence.Projection = choice.Remaining;
            if (choice.Placed)
                sequence.Projection.Add(new(result, data.Recipe.Monster.MapId, choice.X, choice.Y, now, false, false));
            sequence.PreparedCount++;
            prepared = result; reason = string.Empty; return true;
        }
    }

    private DragonhowlGroundChoice ChooseDragonhowlGround(
        WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data,
        DragonhowlGroundTerrain terrain, IReadOnlyList<DragonhowlGroundCell> before,
        DragonhowlFloorImage image, DateTimeOffset now)
    {
        var expired = before.Where(row => !row.Pickup && !row.NonExpiring && now >= row.CreatedAt + LegacyLifetime)
            .Select(row => row.Identity).ToArray();
        var remaining = before.Where(row => !expired.Contains(row.Identity, ReferenceEqualityComparer.Instance)).ToList();
        bool capacity = image.GroundCount + image.PickupCount < _capacity &&
            (remaining.Count < _capacity || remaining.Any(row => !row.Pickup));
        ushort x = data.OriginX, y = data.OriginY;
        bool placed = capacity && CanPlace(x, y);
        if (capacity && !placed)
        {
            int radius = 1 + data.ScatterRange;
            int minX = Math.Max(0, x - radius), minY = Math.Max(0, y - radius);
            int maxX = Math.Min(ushort.MaxValue, x + radius), maxY = Math.Min(ushort.MaxValue, y + radius);
            for (int cy = minY; cy <= maxY && !placed; cy++)
                for (int cx = minX; cx <= maxX; cx++)
                    if (CanPlace((ushort)cx, (ushort)cy))
                    { x = (ushort)cx; y = (ushort)cy; placed = true; break; }
        }
        // No-ground is the existing null-reservation outcome: expiration can
        // inform placement, but it performs no live floor transition. Preserve
        // those same cells for subsequent locally planned attempts as well.
        if (!placed) return new(false, x, y, before.ToList(), [], null);
        object? evicted = null;
        if (placed && remaining.Count >= _capacity)
        {
            int index = remaining.FindIndex(row => !row.Pickup);
            if (index < 0) throw new InvalidOperationException("ownedDragonhowlGroundCapacityChanged");
            evicted = remaining[index].Identity; remaining.RemoveAt(index);
        }
        return new(placed, x, y, remaining, expired, evicted);

        bool CanPlace(ushort cx, ushort cy)
        {
            long map = data.Recipe.Monster.MapId;
            return !(data.IsMoney && (cx == 0 || cy == 0)) &&
                !image.GroundCells.Any(row => row.MapId == map && row.X == cx && row.Y == cy) &&
                !remaining.Any(row => row.MapId == map && row.X == cx && row.Y == cy) && terrain.IsWalkable(cx, cy);
        }
    }

    internal sealed record DragonhowlGroundCell(object Identity, long MapId, ushort X, ushort Y,
        DateTimeOffset CreatedAt, bool NonExpiring, bool Pickup);
    internal sealed record DragonhowlGroundChoice(bool Placed, ushort X, ushort Y,
        List<DragonhowlGroundCell> Remaining, object[] Expired, object? Evicted)
    {
        internal bool SameOutcome(DragonhowlGroundChoice other) => Placed == other.Placed &&
            (!Placed || X == other.X && Y == other.Y) && ReferenceEquals(Evicted, other.Evicted) &&
            Expired.SequenceEqual(other.Expired, ReferenceEqualityComparer.Instance);
    }

    // Exact captured runtime graph, including mutable state contents and order.
    // A new image is captured only initially or after this concrete sequence
    // has actually committed its own floor transition under _gate.
    internal sealed class DragonhowlFloorImage
    {
        private readonly (uint Uid, FloorItemRuntimeState State, FloorItemRuntimeEntry Entry,
            LinkedListNode<uint> Node, DateTimeOffset CreatedAt)[] visible;
        private readonly KeyValuePair<uint, GroundReservation>[] ground;
        private readonly KeyValuePair<uint, PickupReservation>[] pickup;
        internal int GroundCount => ground.Length;
        internal int PickupCount => pickup.Length;
        internal readonly (long MapId, ushort X, ushort Y)[] GroundCells;
        internal DragonhowlFloorImage(FloorItemRuntimeStateStore store)
        {
            if (store._visibleByUid.Count != store._insertionOrder.Count)
                throw new InvalidOperationException("ownedDragonhowlFloorOrderInvalid");
            visible = store._insertionOrder.Select(uid =>
            {
                var state = store._visibleByUid[uid];
                return (uid, state, state.Entry, state.OrderNode, state.CreatedAt);
            }).ToArray();
            ground = store._pendingGroundByUid.ToArray(); pickup = store._pendingPickupByUid.ToArray();
            GroundCells = ground.Select(pair => (pair.Value.Entry.MapId, pair.Value.Entry.Packet.X, pair.Value.Entry.Packet.Y)).ToArray();
        }
        internal bool Matches(FloorItemRuntimeStateStore store) =>
            store._visibleByUid.Count == visible.Length && store._insertionOrder.SequenceEqual(visible.Select(row => row.Uid)) &&
            visible.All(row => store._visibleByUid.TryGetValue(row.Uid, out var actual) && ReferenceEquals(actual, row.State) &&
                ReferenceEquals(actual.Entry, row.Entry) && ReferenceEquals(actual.OrderNode, row.Node) && actual.CreatedAt == row.CreatedAt) &&
            store._pendingGroundByUid.Count == ground.Length && ground.All(row =>
                store._pendingGroundByUid.TryGetValue(row.Key, out var actual) && ReferenceEquals(actual, row.Value)) &&
            store._pendingPickupByUid.Count == pickup.Length && pickup.All(row =>
                store._pendingPickupByUid.TryGetValue(row.Key, out var actual) && ReferenceEquals(actual, row.Value));
        internal List<DragonhowlGroundCell> Cells(DragonhowlGroundSequence sequence) => visible.Select(row =>
            new DragonhowlGroundCell(sequence.Identity(row.State), row.Entry.MapId, row.Entry.Packet.X, row.Entry.Packet.Y,
                row.CreatedAt, row.Entry.Effect?.ExpiresAt is not null, pickup.Any(pair => pair.Key == row.Uid))).ToList();
    }

    internal sealed class DragonhowlGroundSequence
    {
        private readonly Dictionary<object, PreparedDragonhowlGround> issuedStates = new(ReferenceEqualityComparer.Instance);
        internal DragonhowlFloorImage Expected;
        internal List<DragonhowlGroundCell> Projection;
        internal int PreparedCount, Next;
        internal bool Failed;
        internal DragonhowlGroundSequence(FloorItemRuntimeStateStore store)
        { Expected = new(store); Projection = Expected.Cells(this); }
        internal object Identity(object state) => issuedStates.TryGetValue(state, out var actual) ? actual : state;
        internal void Record(object state, PreparedDragonhowlGround owner) => issuedStates.Add(state, owner);
        internal bool IsCurrent(FloorItemRuntimeStateStore store) => !Failed && Expected.Matches(store);
    }

    internal sealed class PreparedDragonhowlGround : IDisposable
    {
        internal readonly FloorItemRuntimeStateStore store;
        internal readonly DragonhowlGroundSequence sequence;
        private readonly WorldSessionStore.OwnedDragonhowlGroundAttempt purpose;
        private readonly WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data;
        private readonly DragonhowlGroundTerrain terrain;
        private readonly DragonhowlGroundChoice choice;
        private readonly DateTimeOffset capturedAt;
        private readonly int index;
        private FloorItemRuntimeState? published;
        private FloorItemRuntimeEntry? committedEntry;
        private bool committing, committed, disposed;
        internal bool HasPlannedGround => choice.Placed;
        internal ushort PlannedX => choice.X;
        internal ushort PlannedY => choice.Y;
        internal FloorItemRuntimeEntry? Entry => committed ? committedEntry : null;

        internal PreparedDragonhowlGround(FloorItemRuntimeStateStore store, WorldSessionStore.OwnedDragonhowlGroundAttempt purpose,
            WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData data, DragonhowlGroundTerrain terrain,
            DragonhowlGroundSequence sequence, DragonhowlGroundChoice choice, DateTimeOffset capturedAt)
        { this.store = store; this.purpose = purpose; this.data = data; this.terrain = terrain;
            this.sequence = sequence; this.choice = choice; this.capturedAt = capturedAt; index = sequence.PreparedCount; }

        internal bool InputsAreCurrentPlain()
        {
            lock (store._gate)
                return !disposed && data.Recipe.Context.TimeProvider is { } clock && clock.GetUtcNow() >= capturedAt &&
                    data.Recipe.InputsAreCurrentPlain() && terrain.IsCurrentPlain() && sequence.IsCurrent(store) &&
                    (!committed || PublishedCurrentNoLock());
        }

        internal bool CoversPayload(WorldPacketHandlers.DragonhowlDropRecipe.DragonhowlDropAttemptData actual, bool placed) =>
            Monitor.IsEntered(store._gate) && committing && !committed && !disposed &&
            ReferenceEquals(data, actual) && choice.Placed == placed && sequence.Next == index;

        internal bool TryCommit(WorldSessionStore.OwnedDragonhowlGroundAttempt actualPurpose)
        {
            if (!ReferenceEquals(purpose, actualPurpose) || !purpose.TryReadCommit(this, out var now) || now < capturedAt) return false;
            lock (store._gate)
            {
                if (committing || committed || sequence.Next != index || !InputsAreCurrentPlain()) return false;
                // Recheck exact expiry/eviction/placement effects at the real
                // phase time. Temporal drift is refusal, not a fresh selection.
                var actualChoice = store.ChooseDragonhowlGround(data, terrain, sequence.Expected.Cells(sequence), sequence.Expected, now);
                if (!choice.SameOutcome(actualChoice) || !terrain.IsCurrentPlain()) return false;
                committing = true;
                try
                {
                    if (!data.TryMaterialize(purpose, this, choice.X, choice.Y, out var entry)) return false;
                    if (entry is not null && (store._pendingGroundByUid.ContainsKey(entry.Packet.Uid) ||
                        store._pendingPickupByUid.ContainsKey(entry.Packet.Uid) ||
                        store._visibleByUid.ContainsKey(entry.Packet.Uid))) return false;
                    // All FINAL packet bytes exist before any floor mutation.
                    if (entry is not null)
                    {
                        store.RemoveExpiredNoLock(now);
                        using var reservation = new GroundReservation(store, entry);
                        store._pendingGroundByUid.Add(entry.Packet.Uid, reservation);
                        if (!reservation.TryPublish(now) || !store._visibleByUid.TryGetValue(entry.Packet.Uid, out var actual) ||
                            !ReferenceEquals(actual.Entry, entry)) return false;
                        published = actual; committedEntry = entry; sequence.Record(actual, this);
                    }
                    sequence.Expected = new(store);
                    sequence.Next++; committed = true;
                    return PublishedCurrentNoLock();
                }
                finally
                {
                    // Post-UID/publication uncertainty is irreversible for this
                    // plan; the enclosing killed owner performs fencing.
                    if (!committed) sequence.Failed = true;
                }
            }
        }

        private bool PublishedCurrentNoLock() => committed && (!choice.Placed || published is not null && committedEntry is not null &&
            store._visibleByUid.TryGetValue(committedEntry.Packet.Uid, out var actual) && ReferenceEquals(actual, published) &&
            ReferenceEquals(actual.Entry, committedEntry));
        internal bool IsCommittedCurrent()
        {
            lock (store._gate) return !disposed && data.Recipe.InputsAreCurrentPlain() && terrain.IsCurrentPlain() && PublishedCurrentNoLock();
        }
        internal byte[] CopyFrame() => Entry is null ? throw new InvalidOperationException("ownedDragonhowlNoGroundHasNoFrame") : data.CopyFrame();
        public void Dispose()
        {
            lock (store._gate)
            {
                if (disposed) return;
                disposed = true;
                if (!committed) sequence.Failed = true;
                // No preflight reservation exists. Published entries and
                // consumed UIDs are never rolled back by token disposal.
            }
        }
    }

    internal sealed class DragonhowlGroundTerrain
    {
        private readonly LegacyGameMapTerrainQuery query;
        private readonly LegacyDMapTerrain terrain;
        private readonly IReadOnlyList<LegacyDMapCell> cells;
        private readonly Dictionary<int, LegacyDMapCell> used = [];
        private readonly int count;
        internal DragonhowlGroundTerrain(LegacyGameMapTerrainQuery query, int mapId)
        {
            this.query = query;
            if (!query.TryGetTerrain(mapId, out var actual) || actual.MapId != mapId || actual.Width <= 0 || actual.Height <= 0)
                throw new InvalidDataException("ownedDragonhowlDropTerrainUnavailable");
            terrain = actual; cells = actual.Cells;
            if (cells.GetType() != typeof(LegacyDMapCell[]) && cells.GetType() != typeof(List<LegacyDMapCell>))
                throw new InvalidDataException("ownedDragonhowlDropTerrainUnsupported");
            count = checked(actual.Width * actual.Height);
            if (cells.Count != count) throw new InvalidDataException("ownedDragonhowlDropTerrainShapeInvalid");
        }
        internal bool IsWalkable(ushort x, ushort y)
        {
            if (x >= terrain.Width || y >= terrain.Height) return false;
            int index = checked(y * terrain.Width + x);
            var cell = cells[index] ?? throw new InvalidDataException("ownedDragonhowlDropTerrainCellMissing");
            if (cell.X != x || cell.Y != y) throw new InvalidDataException("ownedDragonhowlDropTerrainCellMisplaced");
            if (used.TryGetValue(index, out var before) && !ReferenceEquals(before, cell))
                throw new InvalidDataException("ownedDragonhowlDropTerrainChanged");
            used[index] = cell; return cell.IsWalkable;
        }
        internal bool IsCurrentPlain() => query.TryGetTerrain(terrain.MapId, out var current) && ReferenceEquals(current, terrain) &&
            ReferenceEquals(current.Cells, cells) && cells.Count == count && used.All(pair => ReferenceEquals(cells[pair.Key], pair.Value));
    }
}
