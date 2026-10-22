#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native proficiency file methods, not SaveClient/ExecuteLogin dispatch.
internal static partial class LegacyNativeServerDatabasePersistence
{
    public unsafe static void LoadClientProfs(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
        if (binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersProfs\\" + client.Player.UID + ".bin", FileMode.Open))
        {
            LoadClientProfsOpenedFile(binaryFile, client);
        }
    }

    internal unsafe static void LoadClientProfsOpenedFile(
        LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        int num = default(int);
        binaryFile.ReadCount(ref num);
        LegacyNativeClientProficiency.DBProf dBProf = default(LegacyNativeClientProficiency.DBProf);
        for (int i = 0; i < num; i++)
        {
            binaryFile.ReadProf(ref dBProf);
            LegacyNativeMsgProficiency clientProf = dBProf.GetClientProf();
            client.MyProfs.ClientProf.TryAdd(clientProf.ID, clientProf);
        }
        binaryFile.Close();
    }

    public unsafe static void SaveClientProfs(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
        if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersProfs\\" + client.Player.UID + ".bin", FileMode.Create))
        {
            return;
        }
        SaveClientProfsOpenedFile(binaryFile, client);
    }

    internal unsafe static void SaveClientProfsOpenedFile(
        LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        LegacyNativeClientProficiency.DBProf dBProf = default(LegacyNativeClientProficiency.DBProf);
        int count = client.MyProfs.ClientProf.Count;
        binaryFile.WriteCount(ref count);
        foreach (LegacyNativeMsgProficiency value in client.MyProfs.ClientProf.Values)
        {
            dBProf.GetDBSpell(value);
            binaryFile.WriteProf(ref dBProf);
        }
        binaryFile.Close();
    }
}
