using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeTradeRuntime
{
    public bool ItemInTrade(LegacyNativeGameItemRuntime Dataitem)
    {
        ArgumentNullException.ThrowIfNull(Dataitem);
        uint uid = Dataitem.Uid;
        if (uid == 0)
            throw new ArgumentException("A trade query requires an item UID.", nameof(Dataitem));
        if (!TryCaptureCanonicalItems(Items, out var offered) ||
            !ItemsStillMatch(Items, offered) || Dataitem.Uid != uid)
            throw new InvalidOperationException("The native trade offer has invalid or changing item identities.");
        return offered.ContainsKey(uid);
    }

    // Read-only observation, not an escrow claim or permission to transfer items.
    public bool ValidItems()
    {
        var player = Owner.Player;
        var inventory = Owner.Inventory;
        var ownedItems = inventory?.ClientItems;
        if (player is null || !ReferenceEquals(player.Owner, Owner) ||
            inventory is null || !ReferenceEquals(inventory.Owner, Owner) ||
            !TryCaptureCanonicalItems(Items, out var offered) ||
            !TryCaptureCanonicalItems(ownedItems, out var owned))
            return false;

        foreach (var pair in offered)
            if (!owned.TryGetValue(pair.Key, out var item) || !ReferenceEquals(item, pair.Value))
                return false;

        return ItemsStillMatch(Items, offered) && ItemsStillMatch(ownedItems, owned) &&
            ReferenceEquals(Owner.Player, player) && ReferenceEquals(Owner.Inventory, inventory) &&
            ReferenceEquals(inventory.ClientItems, ownedItems);
    }

    private static bool TryCaptureCanonicalItems(
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? source,
        out Dictionary<uint, LegacyNativeGameItemRuntime> snapshot)
    {
        snapshot = new();
        if (source is null)
            return false;
        foreach (var pair in source.ToArray())
            if (pair.Key == 0 || pair.Value is null || pair.Value.Uid != pair.Key ||
                !snapshot.TryAdd(pair.Key, pair.Value))
                return false;
        return true;
    }

    private static bool ItemsStillMatch(ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? source,
        Dictionary<uint, LegacyNativeGameItemRuntime> expected)
    {
        if (!TryCaptureCanonicalItems(source, out var current) || current.Count != expected.Count)
            return false;
        foreach (var pair in expected)
            if (!current.TryGetValue(pair.Key, out var item) || !ReferenceEquals(item, pair.Value))
                return false;
        return true;
    }
}
