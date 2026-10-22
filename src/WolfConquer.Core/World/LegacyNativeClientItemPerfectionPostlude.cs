using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Literal LoadClientItems1302..1351 suffix, not a LoadClientItems facade.
// Reader/count/deposited map are the SAME instances owned by the preceding native loop.
// Only conditionally usable after the genuine container candidate is integrated.
internal static class LegacyNativeClientItemPerfectionPostlude
{
    internal static void ReadAndApply(
        LegacyNativeItemRecordFile binaryFile,
        LegacyNativeGameClientRuntime client,
        Dictionary<uint, LegacyNativeGameItemRuntime> deposited,
        ref int count)
    {
        _ = binaryFile.ReadCount(ref count);
        for (int index = 0; index < count; index++)
        {
            LegacyNativeClientItemPerfectionRecord perfection = default;
            _ = binaryFile.ReadPerfection(ref perfection);
            if (deposited.ContainsKey(perfection.ItemUID))
            {
                LegacyNativeGameItemRuntime item = deposited[perfection.ItemUID];
                perfection.ApplyTo(item);
                continue;
            }
            if (client.Equipment!.ClientItems.ContainsKey(perfection.ItemUID))
            {
                LegacyNativeGameItemRuntime item = client.Equipment.ClientItems[perfection.ItemUID];
                perfection.ApplyTo(item);
                continue;
            }
            if (client.Inventory!.ClientItems.ContainsKey(perfection.ItemUID))
            {
                LegacyNativeGameItemRuntime item = client.Inventory.ClientItems[perfection.ItemUID];
                perfection.ApplyTo(item);
                continue;
            }
            foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> bank in client.Warehouse!.ClientItems.Values)
            {
                if (bank.ContainsKey(perfection.ItemUID))
                {
                    LegacyNativeGameItemRuntime item = bank[perfection.ItemUID];
                    perfection.ApplyTo(item);
                    break;
                }
            }
        }
        // Native has NO finally: any read/lookup/getter/store fault skips this Close.
        binaryFile.Close();
    }
}
