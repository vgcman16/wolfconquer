using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;
using Placement = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Placement;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeItemLoadMaterialization
{
    internal static Destination RequiredDestinations(LegacyNativeItemLoadImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ushort gearSlot = LegacyNativeGameItemRuntime.MythsoulBag;
        Destination required = Destination.None;
        var perfectionTargets = new HashSet<uint>();
        foreach (var root in image.Roots)
        {
            if (root.Record.ITEM_ID == 205_901)
            {
                if (root.Deposits.Count != 0) throw Invalid("filtered 205901 deposits need an explicit migration policy");
                continue;
            }
            Destination route = ClassifyRoot(root.Record, gearSlot);
            required |= route;
            if ((route & (Destination.Inventory | Destination.Equipment | Destination.Warehouse)) != 0)
                perfectionTargets.Add(root.Record.UID);
            // Actual old lookup and saved child graph retain genuine child targets.
            foreach (var child in root.Deposits) perfectionTargets.Add(child.UID);
        }
        foreach (var perfection in image.Perfections)
            if (!perfectionTargets.Contains(perfection.ItemUID))
                throw Invalid("perfection targets a filtered or unsupported main container");
        return required;
    }

    private static Destination ClassifyRoot(LegacyNativeClientItemRecord root, ushort gearSlot)
    {
        if (root.WH_ID != 0)
        {
            if (root.WH_ID != 100) return Destination.Warehouse;
            Destination wardrobe = LegacyNativeItemPosition.ItemPosition(root.ITEM_ID) switch
            {
                9 => Destination.WardrobeGarment,
                17 => Destination.WardrobeMount,
                _ => throw Invalid("WH100 item has no genuine wardrobe bucket")
            };
            return root.Position is > 0 and <= 59 ? wardrobe | Destination.Equipment : wardrobe;
        }
        Destination result = root.Position == gearSlot ? Destination.Gear : Destination.None;
        if (root.Position is >= 213 and <= 215) result |= Destination.Relic;
        else if (root.Position is > 0 and <= 63) result |= Destination.Equipment;
        else if (root.Position == 0) result |= Destination.Inventory;
        else if (root.Position is 211 or 212 or >= 101 and <= 135) result |= Destination.Rune;
        if (result == Destination.None) throw Invalid("unrepresentable root location");
        return result;
    }

    private static void PreparePlacements(LegacyNativeGameItemRuntime item, LegacyNativeClientItemRecord root,
        MaterializationWitness witness, List<Placement> placements)
    {
        Destination route = ClassifyRoot(root, witness.GearSlot);
        if (root.WH_ID == 100)
        {
            var wardrobe = witness.Wardrobe ?? throw Invalid("missing actual wardrobe");
            Destination actual = wardrobe.GetItemType(item.ITEM_ID) switch
            {
                LegacyNativeWardrobeRuntime.ItemsType.Garment => Destination.WardrobeGarment,
                LegacyNativeWardrobeRuntime.ItemsType.Mount => Destination.WardrobeMount,
                _ => throw Invalid("actual wardrobe refused its item type")
            };
            if ((route & actual) == 0) throw Invalid("wardrobe classification changed");
            placements.Add(new(item, actual));
            if ((route & Destination.Equipment) != 0) placements.Add(new(item, Destination.Equipment));
            return;
        }
        if ((route & Destination.Warehouse) != 0)
        {
            if (witness.GuildId == 0) item.Inscribed = 0;
            placements.Add(new(item, Destination.Warehouse, root.WH_ID));
            return;
        }
        if ((route & Destination.Gear) != 0) placements.Add(new(item, Destination.Gear));
        if ((route & Destination.Relic) != 0) placements.Add(new(item, Destination.Relic));
        else if ((route & Destination.Equipment) != 0)
        {
            if (witness.GuildId == 0) item.Inscribed = 0;
            placements.Add(new(item, Destination.Equipment));
        }
        else if ((route & Destination.Inventory) != 0)
        {
            if (witness.GuildId == 0) item.Inscribed = 0;
            placements.Add(new(item, Destination.Inventory));
        }
        else if ((route & Destination.Rune) != 0)
        {
            witness.PrepareRunePosition(item);
            placements.Add(new(item, Destination.Rune));
        }
    }
}
