#nullable disable

using System;
using System.Threading;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Networking;

public class LegacyNativeBruteforceProtection
{
	private LegacyNativeDbSafeDictionary<string, LegacyNativeBruteForceEntry> collection = new LegacyNativeDbSafeDictionary<string, LegacyNativeBruteForceEntry>();

	private int BanOnWatch;

	private void _internalInit()
	{
		while (true)
		{
			DateTime now = DateTime.Now;
			LegacyNativeBruteForceEntry[] values = collection.GetValues();
			foreach (LegacyNativeBruteForceEntry bruteForceEntry in values)
			{
				if (bruteForceEntry.AddedTimeRemove <= now)
				{
					collection.Remove(bruteForceEntry.IPAddress);
				}
				else if (bruteForceEntry.Unbantime.Value() != 0 && bruteForceEntry.Unbantime.Value() <= now.Value())
				{
					collection.Remove(bruteForceEntry.IPAddress);
				}
			}
			Thread.Sleep(1000);
		}
	}

	public void Init(int WatchBeforeBan)
	{
		BanOnWatch = WatchBeforeBan;
		new Thread(_internalInit).Start();
	}

	public void AddWatch(string IPAddress)
	{
		lock (collection)
		{
			if (!collection.TryGetValue(IPAddress, out var value))
			{
				value = new LegacyNativeBruteForceEntry();
				value.IPAddress = IPAddress;
				value.WatchCheck = 1;
				value.AddedTimeRemove = DateTime.Now.AddMinutes(3.0);
				value.Unbantime = new DateTime(0L);
				collection.Add(IPAddress, value);
			}
			else
			{
				value.WatchCheck++;
				if (value.WatchCheck >= BanOnWatch)
				{
					value.Unbantime = DateTime.Now.AddMinutes(3.0);
				}
			}
		}
	}

	public bool AllowAddress(string IPAddress)
	{
		foreach (LegacyNativeGroupServerList.Server value in LegacyNativeGroupServerList.GroupServers.Values)
		{
			if (value.IPAddress == IPAddress)
			{
				return false;
			}
		}
		return false;
	}

	public bool IsBanned(string IPAddress)
	{
		bool result = false;
		if (collection.TryGetValue(IPAddress, out var value))
		{
			result = value.Unbantime.Value() != 0;
		}
		return result;
	}
}
