namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSystemBannedAccount
{
    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("BanUID.txt");
        foreach (Client value in BannedPoll.Values)
        {
            write.Add(value.ToString());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }

    public static void Load()
    {
        using LegacyNativeDbRead read = new LegacyNativeDbRead("BanUID.txt");
        if (read.Reader())
        {
            uint count = (uint)read.Count;
            for (uint num = 0u; num < count; num++)
            {
                LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
                Client client = new Client();
                client.UID = readLine.Read(0u);
                client.Hours = readLine.Read(0u);
                client.StartBan = readLine.Read(0L);
                client.Name = readLine.Read("");
                client.Reason = readLine.Read("");
                BannedPoll.Add(client.UID, client);
            }
        }
    }
}
