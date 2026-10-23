using System.Collections.Concurrent;
using System.Collections.Generic;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native Database.RechargeShop, including original mutable cache and file IO.
internal static class LegacyNativeRechargeShop
{
    public static ConcurrentDictionary<uint, LegacyNativePlayerRuntime.RechargeType> RechargeAccounts = new ConcurrentDictionary<uint, LegacyNativePlayerRuntime.RechargeType>();

    public static void UpdateRecharge(LegacyNativeGameClientRuntime user)
    {
        if (RechargeAccounts.ContainsKey(user.Player.UID))
        {
            RechargeAccounts[user.Player.UID] = user.Player.RechargeProgress;
        }
        else
        {
            RechargeAccounts.TryAdd(user.Player.UID, user.Player.RechargeProgress);
        }
    }

    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("RechargeShop.txt");
        KeyValuePair<uint, LegacyNativePlayerRuntime.RechargeType>[] array = RechargeAccounts.ToArray();
        KeyValuePair<uint, LegacyNativePlayerRuntime.RechargeType>[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            KeyValuePair<uint, LegacyNativePlayerRuntime.RechargeType> keyValuePair = array2[i];
            write.Add(keyValuePair.Key + "/" + keyValuePair.Value);
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }

    public static void Load()
    {
        using LegacyNativeDbRead read = new LegacyNativeDbRead("RechargeShop.txt");
        if (!read.Reader())
        {
            return;
        }
        int count = read.Count;
        for (uint num = 0u; num < count; num++)
        {
            LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
            uint key = readLine.Read(0u);
            LegacyNativePlayerRuntime.RechargeType value = (LegacyNativePlayerRuntime.RechargeType)readLine.Read(0uL);
            if (!RechargeAccounts.ContainsKey(key))
            {
                RechargeAccounts.TryAdd(key, value);
            }
        }
    }
}
