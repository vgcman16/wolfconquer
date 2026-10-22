using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Only native LoadClientItems1244..1264. No caller/full loader/birth/Rune job facade.
// true replaces that branch's outer-loop continue; false MUST reach remaining native rules.
internal static class LegacyNativeClientItemWarehouseRouting
{
    internal static bool TryRouteWarehouseRecord(
        LegacyNativeGameClientRuntime client, ref LegacyNativeClientItemRecord record,
        LegacyNativeGameItemRuntime item)
    {
        if (record.WH_ID != 0)
        {
            if (record.WH_ID == 100)
            {
                client.MyWardrobe!.AddItem(item);
                if (record.Position > 0 && record.Position <= 59)
                {
                    client.Equipment!.ClientItems.TryAdd(record.UID, item);
                }
                return true;
            }
            if (!client.Warehouse!.ClientItems.ContainsKey(record.WH_ID))
            {
                client.Warehouse!.ClientItems.TryAdd(record.WH_ID,
                    new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>());
            }
            if (client.Player.GuildID == 0)
            {
                item.Inscribed = 0;
            }
            client.Warehouse!.ClientItems[record.WH_ID].TryAdd(record.UID, item);
            return true;
        }
        return false;
    }
}
