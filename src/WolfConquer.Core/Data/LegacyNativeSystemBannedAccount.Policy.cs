using System;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSystemBannedAccount
{
    public static void AddBan(uint UID, string? name, uint Hours, string? Reason = "")
    {
        if (!BannedPoll.ContainsKey(UID))
        {
            Client client = new Client();
            client.UID = UID;
            client.Hours = Hours;
            client.Name = name;
            client.StartBan = DateTime.Now.Ticks;
            client.Reason = Reason;
            BannedPoll.Add(client.UID, client);
        }
    }

    public static void AddBan(LegacyNativeGameClientRuntime client, uint Hours, string? Reason = "")
    {
        if (!BannedPoll.ContainsKey(client.Player.UID))
        {
            Client client2 = new Client();
            client2.UID = client.Player.UID;
            client2.Hours = Hours;
            client2.Name = client.Player.Name;
            client2.StartBan = DateTime.Now.Ticks;
            client2.Reason = Reason;
            BannedPoll.Add(client2.UID, client2);
            client.Player.BanCount++;
        }
    }

    public static void RemoveBan(uint UID)
    {
        if (BannedPoll.ContainsKey(UID))
        {
            BannedPoll.Remove(UID);
        }
    }

    public static void RemoveBan(string? name)
    {
        uint num = 0u;
        foreach (Client value in BannedPoll.Values)
        {
            if (value.Name == name)
            {
                num = value.UID;
                break;
            }
        }
        if (num != 0)
        {
            RemoveBan(num);
        }
    }

    public static bool IsBanned(uint UID, out string Messaj)
    {
        if (BannedPoll.ContainsKey(UID))
        {
            Client client = BannedPoll[UID];
            if (!(DateTime.FromBinary(client.StartBan).AddHours(client.Hours) < DateTime.Now))
            {
                TimeSpan timeSpan = TimeSpan.FromTicks(DateTime.FromBinary(client.StartBan).AddHours(client.Hours).Ticks) - TimeSpan.FromTicks(DateTime.Now.Ticks);
                Messaj = timeSpan.Days + " Days, " + timeSpan.Hours + " Hours and " + timeSpan.Minutes + " Minutes. (Due to " + client.Reason + ")";
                return true;
            }
            BannedPoll.Remove(client.UID);
        }
        Messaj = "";
        return false;
    }
}
