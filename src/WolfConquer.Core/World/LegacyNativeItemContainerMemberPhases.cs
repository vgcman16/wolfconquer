namespace WolfConquer.Core.World;

// Explicit native slots. No caller is installed in login, host birth or raw loading here.
// Separate slot calls do NOT recreate omitted GameClient initializers/constructor effects.
internal static class LegacyNativeItemContainerMemberPhases
{
    // GameClient field initializer29: before its remaining field initializers and ctor body.
    internal static void InitializeGearMythsoulBagAtNativeFieldSlot(LegacyNativeGameClientRuntime client)
    {
        client.GearMythsoulBag = new LegacyNativeItemBagRuntime();
    }

    // GameClient field initializer269: still BEFORE constructor Wardrobe1390/Player1412.
    internal static void InitializeRelicFuseAtNativeFieldSlot(LegacyNativeGameClientRuntime client)
    {
        client.RelicFuse = new LegacyNativeItemBagRuntime();
    }

    // Native constructor1390, after MyExchangeShop and before Activeness. Not called by current birth.
    internal static void AssignWardrobeAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.MyWardrobe = new LegacyNativeWardrobeRuntime(client);
    }

    // Native GameClient1395, AFTER ArenaStatistic and BEFORE Confiscator/Player1412.
    // Explicit and unwired: do not call from current partial birth or DataCore item-owner prefix.
    internal static void AssignDemonExterminatorAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.DemonExterminator = new LegacyNativeDemonExterminatorRuntime();
    }

    // Literal contiguous DataCore.LoadClient82..85 prefix. Owner is re-read per native expression.
    // Profs, Spells, Mail, Achievement and the real caller AFTER this prefix remain absent.
    internal static void AssignLoadClientItemOwnersPrefix(LegacyNativePlayerRuntime player)
    {
        player.Owner.Rune = new LegacyNativeRuneItemStorage(player.Owner);
        player.Owner.Inventory = new LegacyNativeInventoryRuntime(player.Owner);
        player.Owner.Equipment = new LegacyNativeEquipItemStorage(player.Owner);
        player.Owner.Warehouse = new LegacyNativeWarehouseRuntime(player.Owner);
    }
}
