using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Native ServerDatabase.SaveClientItems1354..1410 over concrete native owners.
// No host/autosave/snapshot caller is installed. This is NOT the managed WAL path:
// native FileMode.Create and partial-write/fault semantics are intentionally kept.
internal static class LegacyNativeClientItemSave
{
    internal static void SaveClientItems(LegacyNativeGameClientRuntime client)
    {
        var file = new LegacyNativeItemRecordFile();
        if (!file.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersItems\\" +
            client.Player.UID + ".bin", FileMode.Create))
            return;
        SaveOpenedFile(file, client);
    }

    // The same opened file and reused DB record survive the entire native body.
    // No finally: a native conversion/enumeration/write fault skips Close.
    internal static void SaveOpenedFile(LegacyNativeItemRecordFile file, LegacyNativeGameClientRuntime client)
    {
        var depositedPerfection = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        var record = default(LegacyNativeClientItemRecord);
        var items = client.AllMyItems().ToArray();
        int count = items.Length;
        _ = file.WriteCount(ref count);
        foreach (var item in items)
        {
            _ = record.GetDBItem(item);
            if (!file.WriteRecord(ref record))
                LegacyNativeConsole.WriteLine("Unable to save an item!", ConsoleColor.DarkRed);
            if (item.Deposite.Count <= 0)
                continue;
            foreach (var child in item.Deposite.Values)
            {
                _ = record.GetDBItem(child);
                _ = file.WriteRecord(ref record);
                // Native deposit predicate deliberately has NO OwnerUID check.
                if ((child.PerfectionLevel != 0 || child.PerfectionProgress != 0) && child.IsEquip &&
                    !depositedPerfection.ContainsKey(child.Uid))
                    depositedPerfection.Add(child.Uid, child);
            }
        }

        var perfectionItems = client.AllPerfectionItems().ToArray();
        count = perfectionItems.Length + depositedPerfection.Count;
        _ = file.WriteCount(ref count);
        foreach (var item in perfectionItems)
        {
            var perfection = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(item);
            if (!file.WritePerfection(ref perfection))
                LegacyNativeConsole.WriteLine("Unable to save an item!", ConsoleColor.DarkRed);
        }
        foreach (var item in depositedPerfection.Values)
        {
            var perfection = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(item);
            if (!file.WritePerfection(ref perfection))
                LegacyNativeConsole.WriteLine("Unable to save an item!", ConsoleColor.DarkRed);
        }
        file.Close();
    }
}
