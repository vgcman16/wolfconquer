using System.Collections.Generic;

namespace WolfConquer.Core.World;

// ServerDatabase.LoadClientItems1167..1243 for ONE outer iteration only.
// Unwired. Caller retains its count/loop and SAME reused DBItem, reader and deposited map.
// A null return means native205901 continue. Routing MUST inspect the escaped record,
// which is the final child after deposits, NOT the returned outer item's fields.
internal static class LegacyNativeClientItemMaterialization
{
    internal static LegacyNativeGameItemRuntime? ReadOuterAndDeposits(
        LegacyNativeItemRecordFile binaryFile,
        LegacyNativeGameClientRuntime client,
        ref LegacyNativeClientItemRecord record,
        Dictionary<uint, LegacyNativeGameItemRuntime> deposited)
    {
        _ = binaryFile.ReadRecord(ref record);
        if (record.ITEM_ID == 750000)
        {
            client.DemonExterminator!.ItemUID = record.UID;
        }
        if (record.ITEM_ID == 205901)
        {
            return null;
        }
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        LegacyNativeGameItemRuntime item = record.GetDataItem();
        if (record.DepositeCount != 0)
        {
            uint depositeCount = record.DepositeCount;
            for (int index = 0; index < depositeCount; index++)
            {
                _ = binaryFile.ReadRecord(ref record);
                if (record.ITEM_ID == 750000)
                {
                    client.DemonExterminator!.ItemUID = record.ITEM_ID;
                }
                LegacyNativeGameItemRuntime child = record.GetDataItem();
                if (client.Player.GuildID == 0)
                {
                    child.Inscribed = 0;
                }
                item.Deposite.TryAdd(child.Uid, child);
                if (!deposited.ContainsKey(child.Uid))
                {
                    deposited.Add(child.Uid, child);
                }
            }
        }
        return item;
    }
}
