using System;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSystemBanned
{
    // Native SystemBanned27..37 intentionally never removes a record.
    public static void RemoveBan(string? IP)
    {
        foreach (Client value in BannedPoll.Values)
        {
            if (value.IP == IP)
            {
                IP = value.IP;
                break;
            }
        }
    }

    public static void AddBan(string? IP, uint Hours)
    {
        Client client = new Client();
        client.IP = IP;
        client.Hours = Hours;
        client.StartBan = DateTime.Now.Ticks;
        BannedPoll.Add(client.IP!, client);
    }

    public static bool IsBanned(string Ip, out string Messaj)
    {
        if (BannedPoll.ContainsKey(Ip))
        {
            Client client = BannedPoll[Ip];
            if (!(DateTime.FromBinary(client.StartBan).AddHours(client.Hours) < DateTime.Now))
            {
                TimeSpan timeSpan = TimeSpan.FromTicks(DateTime.FromBinary(client.StartBan).AddHours(client.Hours).Ticks) - TimeSpan.FromTicks(DateTime.Now.Ticks);
                Messaj = " " + timeSpan.Days + " Days " + timeSpan.Hours + " Hours " + timeSpan.Minutes + " Minutes";
                return true;
            }
            BannedPoll.Remove(client.IP!);
        }
        Messaj = "";
        return false;
    }
}
