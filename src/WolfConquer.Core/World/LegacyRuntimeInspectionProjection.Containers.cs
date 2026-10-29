using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal static partial class LegacyRuntimeInspectionProjection
{
    internal sealed record WardrobeAlias(uint Uid, int Bucket, LegacyNativeWardrobeRuntime Owner,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] Maps,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Garments,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Mounts);

    private static void RequireSelectedPlacement(string container, uint itemId, ushort position, uint warehouse,
        HashSet<ushort> equipmentSlots)
    {
        if (container == "inventory")
        {
            if (position != 0 || warehouse != 0) throw Invalid();
            return;
        }
        // Exact existing loader/save-graph container membership, not guessed
        // gameplay equip eligibility or a narrowed list of item-family slots.
        if (container != "equipment" ||
            !(warehouse == 0 && position is > 0 and <= 63 ||
              warehouse == 100 && position is > 0 and <= 59 && WardrobeBucket(itemId) >= 0) ||
            !equipmentSlots.Add(position)) throw Invalid();
    }

    private static int WardrobeBucket(uint itemId) => LegacyNativeItemPosition.ItemPosition(itemId) switch
    { 9 => 0, 17 => 1, _ => -1 };

    private static WardrobeAlias CaptureSelectedAlias(LegacyNativeGameClientRuntime client, LegacyNativeGameItemRuntime item)
    {
        var owner = client.MyWardrobe ?? throw Invalid(); var maps = owner.Items;
        if (!owner.IsOwnedBy(client) || maps is null || maps.Length != 2 || maps[0] is not { } garments ||
            maps[1] is not { } mounts || ReferenceEquals(garments, mounts) ||
            !ReferenceEquals(garments.Comparer, EqualityComparer<uint>.Default) ||
            !ReferenceEquals(mounts.Comparer, EqualityComparer<uint>.Default)) throw Invalid();
        var bucket = WardrobeBucket(item.ITEM_ID);
        if (bucket < 0) throw Invalid();
        var selected = bucket == 0 ? garments : mounts; var other = bucket == 0 ? mounts : garments;
        // Only this collected equipment alias is witnessed. No uncollected
        // wardrobe rows are enumerated or represented as a complete snapshot.
        if (!selected.TryGetValue(item.Uid, out var actual) || !ReferenceEquals(actual, item) || other.ContainsKey(item.Uid) ||
            !owner.IsOwnedBy(client) || !ReferenceEquals(owner.Items, maps) ||
            !ReferenceEquals(maps[0], garments) || !ReferenceEquals(maps[1], mounts)) throw Invalid();
        return new(item.Uid, bucket, owner, maps, garments, mounts);
    }

    private static bool SameSelectedAliases(WardrobeAlias[] first, WardrobeAlias[] second) =>
        first.Length == second.Length && first.Zip(second).All(pair => pair.First.Uid == pair.Second.Uid &&
            pair.First.Bucket == pair.Second.Bucket && ReferenceEquals(pair.First.Owner, pair.Second.Owner) &&
            ReferenceEquals(pair.First.Maps, pair.Second.Maps) && ReferenceEquals(pair.First.Garments, pair.Second.Garments) &&
            ReferenceEquals(pair.First.Mounts, pair.Second.Mounts));
}
