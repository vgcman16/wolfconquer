using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyMonsterDropPlacement
{
    public const long LegacyVisibilityDistance = 18;

    public static LegacyMonsterDropPlacementProjection Project(
        LegacyMonsterInstance monster,
        CharacterSnapshot killer,
        LegacyMonsterDeathDropSelection selection,
        LegacyMonsterDropPlacementRolls rolls,
        LegacyMonsterDropPlacementContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(killer);
        ArgumentNullException.ThrowIfNull(selection);

        context ??= LegacyMonsterDropPlacementContext.Default;

        if (selection.Kind == LegacyMonsterDeathDropKind.None)
        {
            return SingleRejection(
                LegacyMonsterDropPlacementStatus.SelectionRejected,
                string.IsNullOrWhiteSpace(selection.Reason)
                    ? "No monster death drop selection was supplied."
                    : selection.Reason,
                monster);
        }

        if (LegacyMonsterDeathDropSelector.IsLegacyMonsterDropBlockedMap(monster.MapId))
        {
            return SingleRejection(
                LegacyMonsterDropPlacementStatus.MapRejected,
                $"Source MonsterRole returns before floor placement on blocked monster-drop map {monster.MapId}.",
                monster);
        }

        if (killer.Uid is < uint.MinValue or > uint.MaxValue)
        {
            return SingleRejection(
                LegacyMonsterDropPlacementStatus.InvalidOwner,
                $"Killer uid {killer.Uid} cannot be projected into source MsgItem.ItemOwnerUID.",
                monster);
        }

        if (selection.ItemId == 0)
        {
            return SingleRejection(
                LegacyMonsterDropPlacementStatus.SelectionRejected,
                "Selected monster drop has no item id to project onto a floor item packet.",
                monster);
        }

        if (selection.Kind == LegacyMonsterDeathDropKind.Money && selection.MoneyAmount == 0)
        {
            return SingleRejection(
                LegacyMonsterDropPlacementStatus.SelectionRejected,
                "Selected monster money drop has no source GenerateGold amount to store for pickup.",
                monster);
        }

        var pending = BuildPendingSourceBehaviors(selection, context);
        var occupied = BuildOccupiedCells(context.OccupiedCells);
        var decisions = new List<LegacyMonsterDropPlacementDecision>();
        var originX = monster.X;
        var originY = monster.Y;
        var placedCount = 0;
        var count = selection.DropCount == 0 ? 1 : selection.DropCount;
        for (var attempt = 0; attempt < count; attempt++)
        {
            var requestedX = originX;
            var requestedY = originY;
            if (!TryResolveGroundCell(
                    monster.MapId,
                    requestedX,
                    requestedY,
                    context.ScatterRange,
                    context.IsGroundValid,
                    occupied,
                    out var placedX,
                    out var placedY))
            {
                decisions.Add(new LegacyMonsterDropPlacementDecision(
                    attempt,
                    LegacyMonsterDropPlacementStatus.NoValidGround,
                    $"Source GameMap.AddGroundItem found no unoccupied valid cell within range {context.ScatterRange} around ({requestedX},{requestedY}).",
                    monster.MapId,
                    requestedX,
                    requestedY,
                    0,
                    0,
                    0,
                    selection.ItemId,
                    ResolvePickupKind(selection),
                    ResolvePickupAmount(selection),
                    checked((uint)killer.Uid),
                    true,
                    FloorItemRuntimeStateStore.LegacyOwnerOnlyDuration,
                    FloorItemRuntimeStateStore.LegacyLifetime,
                    null,
                    null,
                    [],
                    pending));
                originX = 0;
                originY = 0;
                continue;
            }

            var floorUid = unchecked(rolls.FirstFloorUid + (uint)placedCount);
            var packet = LegacyFloorItemPacket.FromMonsterDrop(
                selection.ItemId,
                placedX,
                placedY,
                killer,
                floorUid);
            var entry = BuildRuntimeEntry(monster.MapId, packet, selection);
            var visibility = ProjectVisibility(killer, entry, context);
            decisions.Add(new LegacyMonsterDropPlacementDecision(
                attempt,
                LegacyMonsterDropPlacementStatus.Placed,
                "Source MonsterRole projects the selected drop through GameMap.AddGroundItem, MsgItem, EnqueueItem, then MsgDropID.Visible.",
                monster.MapId,
                requestedX,
                requestedY,
                placedX,
                placedY,
                floorUid,
                selection.ItemId,
                entry.Kind,
                entry.Amount,
                packet.ItemOwnerUid,
                entry.OwnerOnly,
                FloorItemRuntimeStateStore.LegacyOwnerOnlyDuration,
                FloorItemRuntimeStateStore.LegacyLifetime,
                packet,
                entry,
                visibility,
                pending));

            occupied.Add(new LegacyMonsterDropOccupiedCell(monster.MapId, placedX, placedY));
            placedCount++;
            originX = placedX;
            originY = placedY;
        }

        return new LegacyMonsterDropPlacementProjection(decisions);
    }

    private static LegacyMonsterDropPlacementProjection SingleRejection(
        LegacyMonsterDropPlacementStatus status,
        string reason,
        LegacyMonsterInstance monster)
    {
        return new LegacyMonsterDropPlacementProjection(
        [
            new LegacyMonsterDropPlacementDecision(
                0,
                status,
                reason,
                monster.MapId,
                monster.X,
                monster.Y,
                0,
                0,
                0,
                0,
                FloorItemRuntimePickupKind.Item,
                0,
                0,
                false,
                TimeSpan.Zero,
                TimeSpan.Zero,
                null,
                null,
                [],
                [])
        ]);
    }

    private static HashSet<LegacyMonsterDropOccupiedCell> BuildOccupiedCells(
        IReadOnlyList<LegacyMonsterDropOccupiedCell> occupiedCells)
    {
        return new HashSet<LegacyMonsterDropOccupiedCell>(occupiedCells);
    }

    private static bool TryResolveGroundCell(
        int mapId,
        ushort originX,
        ushort originY,
        byte range,
        Func<int, ushort, ushort, bool>? isGroundValid,
        HashSet<LegacyMonsterDropOccupiedCell> occupied,
        out ushort x,
        out ushort y)
    {
        if (CanPlaceGroundItem(mapId, originX, originY, isGroundValid, occupied))
        {
            x = originX;
            y = originY;
            return true;
        }

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
                if (CanPlaceGroundItem(mapId, checkedX, checkedY, isGroundValid, occupied))
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

    private static bool CanPlaceGroundItem(
        int mapId,
        ushort x,
        ushort y,
        Func<int, ushort, ushort, bool>? isGroundValid,
        HashSet<LegacyMonsterDropOccupiedCell> occupied)
    {
        return !occupied.Contains(new LegacyMonsterDropOccupiedCell(mapId, x, y)) &&
            (isGroundValid?.Invoke(mapId, x, y) ?? true);
    }

    private static FloorItemRuntimeEntry BuildRuntimeEntry(
        int mapId,
        LegacyFloorItemPacket packet,
        LegacyMonsterDeathDropSelection selection)
    {
        return selection.Kind == LegacyMonsterDeathDropKind.Money
            ? FloorItemRuntimeEntry.ForMoney(mapId, packet, selection.MoneyAmount, ownerOnly: true)
            : FloorItemRuntimeEntry.ForItem(mapId, packet, ownerOnly: true);
    }

    private static FloorItemRuntimePickupKind ResolvePickupKind(LegacyMonsterDeathDropSelection selection)
    {
        return selection.Kind == LegacyMonsterDeathDropKind.Money
            ? FloorItemRuntimePickupKind.Money
            : FloorItemRuntimePickupKind.Item;
    }

    private static long ResolvePickupAmount(LegacyMonsterDeathDropSelection selection)
    {
        return selection.Kind == LegacyMonsterDeathDropKind.Money
            ? selection.MoneyAmount
            : 0;
    }

    private static IReadOnlyList<string> BuildPendingSourceBehaviors(
        LegacyMonsterDeathDropSelection selection,
        LegacyMonsterDropPlacementContext context)
    {
        var pending = new List<string>();
        if (context.IsGroundValid is null)
        {
            pending.Add("Static terrain Valid-flag validation was not supplied; source GameMap.AddGroundItem validity is assumed true.");
        }

        if (selection.Kind is LegacyMonsterDeathDropKind.Item or LegacyMonsterDeathDropKind.SpecialItem)
        {
            pending.Add("Source DropItem item-base projection for color, durability, plus, bless, sockets, and DBItem durability remains pending.");
        }

        if (selection.Kind == LegacyMonsterDeathDropKind.SpecialItem)
        {
            pending.Add("Source special-drop/equipment stop rules remain selection-layer behavior; placement only projects the supplied selected item.");
        }

        return pending.ToArray();
    }

    private static IReadOnlyList<LegacyMonsterDropVisibilityDecision> ProjectVisibility(
        CharacterSnapshot killer,
        FloorItemRuntimeEntry entry,
        LegacyMonsterDropPlacementContext context)
    {
        var viewers = new List<LegacyMonsterDropObserver>
        {
            new(killer.Uid, killer.MapId, killer.X, killer.Y, context.DynamicId),
        };
        foreach (var observer in context.Observers)
        {
            if (observer.Uid != killer.Uid)
            {
                viewers.Add(observer);
            }
        }

        var decisions = new List<LegacyMonsterDropVisibilityDecision>(viewers.Count);
        foreach (var viewer in viewers)
        {
            var visible = TryExplainVisibility(viewer, entry, context.DynamicId, out var reason);
            var ownerLocked = visible &&
                entry.OwnerOnly &&
                entry.Packet.ItemOwnerUid != 0 &&
                viewer.Uid != entry.Packet.ItemOwnerUid;
            if (ownerLocked)
            {
                reason += " Pickup is owner-locked for the source 20-second ExpireMySelf window; team pickup exceptions are pending runtime policy.";
            }

            decisions.Add(new LegacyMonsterDropVisibilityDecision(
                viewer.Uid,
                viewer.MapId,
                viewer.X,
                viewer.Y,
                viewer.DynamicId,
                visible,
                ownerLocked,
                reason));
        }

        return decisions;
    }

    private static bool TryExplainVisibility(
        LegacyMonsterDropObserver viewer,
        FloorItemRuntimeEntry entry,
        uint dropDynamicId,
        out string reason)
    {
        if (viewer.DynamicId != dropDynamicId)
        {
            reason = "Source MsgItem.CanSee rejects viewers on a different DynamicID.";
            return false;
        }

        if (viewer.MapId != entry.MapId)
        {
            reason = "Source MsgItem.CanSee rejects viewers on a different map.";
            return false;
        }

        var distance = LegacyGetDistance(viewer.X, viewer.Y, entry.Packet.X, entry.Packet.Y);
        if (distance >= LegacyVisibilityDistance)
        {
            reason = "Source MsgItem.CanSee requires distance less than 18 tiles.";
            return false;
        }

        reason = "Source MsgItem.SendAll broadcasts MsgDropID.Visible to same-dynamic-map viewers within 18 tiles.";
        return true;
    }

    private static long LegacyGetDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}

public sealed record LegacyMonsterDropPlacementRolls(uint FirstFloorUid);

public sealed record LegacyMonsterDropPlacementContext
{
    public static LegacyMonsterDropPlacementContext Default { get; } = new();

    public Func<int, ushort, ushort, bool>? IsGroundValid { get; init; }

    public IReadOnlyList<LegacyMonsterDropOccupiedCell> OccupiedCells { get; init; } = [];

    public IReadOnlyList<LegacyMonsterDropObserver> Observers { get; init; } = [];

    public byte ScatterRange { get; init; }

    public uint DynamicId { get; init; }
}

public sealed record LegacyMonsterDropPlacementProjection(
    IReadOnlyList<LegacyMonsterDropPlacementDecision> Decisions)
{
    public IReadOnlyList<LegacyMonsterDropPlacementDecision> Placed =>
        Decisions.Where(static decision => decision.Status == LegacyMonsterDropPlacementStatus.Placed).ToArray();
}

public sealed record LegacyMonsterDropPlacementDecision(
    int AttemptIndex,
    LegacyMonsterDropPlacementStatus Status,
    string Reason,
    int MapId,
    ushort OriginX,
    ushort OriginY,
    ushort X,
    ushort Y,
    uint FloorUid,
    uint ItemId,
    FloorItemRuntimePickupKind PickupKind,
    long Amount,
    uint OwnerUid,
    bool OwnerOnly,
    TimeSpan OwnerOnlyDuration,
    TimeSpan Lifetime,
    LegacyFloorItemPacket? Packet,
    FloorItemRuntimeEntry? RuntimeEntry,
    IReadOnlyList<LegacyMonsterDropVisibilityDecision> Visibility,
    IReadOnlyList<string> PendingSourceBehaviors);

public sealed record LegacyMonsterDropVisibilityDecision(
    long ViewerUid,
    long ViewerMapId,
    long ViewerX,
    long ViewerY,
    uint ViewerDynamicId,
    bool Visible,
    bool PickupOwnerLocked,
    string Reason);

public readonly record struct LegacyMonsterDropOccupiedCell(int MapId, ushort X, ushort Y);

public sealed record LegacyMonsterDropObserver(
    long Uid,
    long MapId,
    long X,
    long Y,
    uint DynamicId = 0);

public enum LegacyMonsterDropPlacementStatus
{
    Placed,
    SelectionRejected,
    MapRejected,
    InvalidOwner,
    NoValidGround,
}
