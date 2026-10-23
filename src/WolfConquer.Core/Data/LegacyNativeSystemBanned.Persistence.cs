namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSystemBanned
{
    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("BanIp.txt");
        foreach (Client value in BannedPoll.Values)
        {
            write.Add(value.ToString());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }

    public static void Load()
    {
        using LegacyNativeDbRead read = new LegacyNativeDbRead("BanIp.txt");
        if (read.Reader())
        {
            uint count = (uint)read.Count;
            for (uint num = 0u; num < count; num++)
            {
                LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
                Client client = new Client();
                client.IP = readLine.Read("");
                client.Hours = readLine.Read(0u);
                client.StartBan = readLine.Read(0L);
                BannedPoll.Add(client.IP!, client);
            }
        }
    }
}
