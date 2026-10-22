namespace WolfConquer.Core.World;

// Only native LoadClientItems1266..1289, AFTER the WH branch has fallen through.
// true means this represented else-if arm completes the outer iteration.
// false MUST continue to the genuine native1290..1300 Rune condition, even after a bag Add.
// No caller, full loader, owner initialization or Rune/job/Class policy is installed here.
internal static class LegacyNativeClientItemNonWarehouseRouting
{
    internal static bool TryCompleteNonWarehouseStoragePrefix(
        LegacyNativeGameClientRuntime client, ref LegacyNativeClientItemRecord record,
        LegacyNativeGameItemRuntime item)
    {
        if (record.Position == LegacyNativeGameItemRuntime.MythsoulBag)
        {
            client.GearMythsoulBag!.Add(item.Uid, item);
        }
        if (record.Position >= 213 && record.Position <= 215)
        {
            client.RelicFuse!.Add(item.Uid, item);
            return true;
        }
        else if (record.Position > 0 && record.Position <= 63)
        {
            if (client.Player.GuildID == 0)
            {
                item.Inscribed = 0;
            }
            client.Equipment!.ClientItems.TryAdd(record.UID, item);
            return true;
        }
        else if (record.Position == 0)
        {
            if (client.Player.GuildID == 0)
            {
                item.Inscribed = 0;
            }
            client.Inventory!.AddDBItem(item);
            return true;
        }
        return false;
    }
}
