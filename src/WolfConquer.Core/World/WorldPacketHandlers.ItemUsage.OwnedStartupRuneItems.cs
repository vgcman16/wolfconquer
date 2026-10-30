using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool MatchesOwnedStartupRuneItems(CharacterSnapshot character, PlayerItemSnapshot before, byte[] expectedAfter)
    {
        // The startup owner already proved lossless item graphs, equal physical
        // UID sets and unchanged Character/Spells. This only reconstructs the
        // source-static item projection; it supplies no catalog authorization.
        if (character.Reborn < 2 || character.Level < 15 || before.RuneItems.Count == 0) return false;
        var after = PlayerItemSnapshotLoader.Parse(expectedAfter);
        if (!TryIndexOwnedRunePositions(before, after, out var positions, out var changed)) return false;
        var firstSame = before.RuneItems.Where(row => row.Position != LegacyRuneBagPosition)
            .GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.First());
        var slots = before.RuneItems.GroupBy(row => row.Position).ToDictionary(group => group.Key,
            group => new OwnedRuneSlot(group.First(), group.OrderBy(row => row.Uid).Take(2).ToArray()));
        var locked = before.RuneItems.Where(row => row.Locked != 0 && row.Position != LegacyRuneBagPosition).ToArray();
        var lockedTypes = locked.Select(row => row.ItemId).ToHashSet();
        var lockedSlots = locked.Select(row => row.Position).ToHashSet();
        // Only the at-most-three changed UIDs can contribute a lasting move.
        // Preserve the displacement order in the key: storage sorting is stable,
        // so the second displaced row precedes the first in the resulting file.
        var projections = new HashSet<OwnedRuneProjection>();
        var canUnequip = HasLegacyInventorySpace(before, 1) && !TryReadLegacyVendorState(character, out _);
        var triedUnchangedUnequip = false;
        foreach (var selected in before.RuneItems)
        {
            if (selected.Locked != 0 || selected.StackSize > 1 || selected.DepositedItems.Count != 0) continue;
            var requested = positions[selected.Uid].After;
            if (canUnequip && requested == LegacyRuneStoragePosition &&
                MatchesOwnedRunePositionChanges(positions, changed, selected, null, null, requested) &&
                (selected.Position != requested || !triedUnchangedUnequip))
            {
                triedUnchangedUnequip |= selected.Position == requested;
                if (HasOwnedRuneItemImage(ReplaceRuneItem(before, selected, selected.WithPosition(requested)), expectedAfter)) return true;
            }
            if (!IsRecordedLegacyV9RuneSlot(selected.ItemId, requested) || lockedTypes.Contains(selected.ItemId) || lockedSlots.Contains(requested)) continue;
            firstSame.TryGetValue(selected.ItemId, out var same);
            slots.TryGetValue(requested, out var slot);
            var displaced = same is null ? slot?.Original : slot?.Sorted.FirstOrDefault(row => row.Uid != same.Uid);
            if (!MatchesOwnedRunePositionChanges(positions, changed, selected, same, displaced, requested) ||
                !projections.Add(OwnedRuneProjection.Create(selected, same, displaced, requested))) continue;
            var items = before;
            TryUnequipFirstMatchingRune(items, row => row.ItemId == selected.ItemId && row.Position != LegacyRuneBagPosition, out items, out _);
            TryUnequipFirstMatchingRune(items, row => row.Position == requested && row.Position != LegacyRuneBagPosition, out items, out _);
            if (TryGetRuneItem(items, selected.Uid, out var chosen) &&
                HasOwnedRuneItemImage(ReplaceRuneItem(items, chosen, chosen.WithPosition(requested)), expectedAfter)) return true;
        }
        return false;
    }

    private static bool TryIndexOwnedRunePositions(PlayerItemSnapshot before, PlayerItemSnapshot after,
        out Dictionary<uint, (ushort Before, ushort After)> positions, out Dictionary<uint, ushort> changed)
    {
        positions = []; changed = [];
        if (before.RuneItems.Count != after.RuneItems.Count) return false;
        var afterPositions = new Dictionary<uint, ushort>();
        foreach (var row in after.RuneItems)
            if (!afterPositions.TryAdd(row.Uid, row.Position)) return false;
        foreach (var row in before.RuneItems)
        {
            if (!afterPositions.TryGetValue(row.Uid, out var position) || !positions.TryAdd(row.Uid, (row.Position, position))) return false;
            if (row.Position != position) changed.Add(row.Uid, position);
        }
        // Equip moves at most the same-ID row, the requested-slot row and the
        // selected row. Unequip moves only its selected row.
        return changed.Count <= 3;
    }

    private static bool MatchesOwnedRunePositionChanges(Dictionary<uint, (ushort Before, ushort After)> positions,
        Dictionary<uint, ushort> changed, PlayerItemSnapshotItem selected, PlayerItemSnapshotItem? same,
        PlayerItemSnapshotItem? slot, ushort requested)
    {
        var projected = new Dictionary<uint, ushort>();
        if (same is not null) projected[same.Uid] = LegacyRuneStoragePosition;
        if (slot is not null) projected[slot.Uid] = LegacyRuneStoragePosition;
        projected[selected.Uid] = requested;
        foreach (var uid in projected.Keys.ToArray())
            if (projected[uid] == positions[uid].Before) projected.Remove(uid);
        return projected.Count == changed.Count && projected.All(pair => changed.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static bool HasOwnedRuneItemImage(PlayerItemSnapshot items, byte[] expected) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(expected);

    // The old v9 Frame producer admitted nine yellow slots. New v12 gameplay
    // fixes its native twelve-slot policy without widening historical intent.
    private static bool IsRecordedLegacyV9RuneSlot(uint itemId, ushort position) => (itemId / 10_000) switch
    {
        401 => position is 101 or 121,
        402 => position is 102 or 122,
        403 => position is (>= 103 and <= 111) or (>= 123 and <= 131),
        _ => false,
    };

    private sealed record OwnedRuneSlot(PlayerItemSnapshotItem Original, PlayerItemSnapshotItem[] Sorted);

    private readonly record struct OwnedRuneProjection(uint? Same, uint? Slot, uint? Selected, ushort Position)
    {
        internal static OwnedRuneProjection Create(PlayerItemSnapshotItem selected, PlayerItemSnapshotItem? same,
            PlayerItemSnapshotItem? slot, ushort requested) => new(
                Stored(same, selected.Uid), Stored(slot, selected.Uid), selected.Position == requested ? null : selected.Uid, requested);

        private static uint? Stored(PlayerItemSnapshotItem? row, uint selectedUid) =>
            row is not null && row.Uid != selectedUid && row.Position != LegacyRuneStoragePosition ? row.Uid : null;
    }
}
