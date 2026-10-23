using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeConfiscatorTable
{
internal unsafe void Save()
	{
		LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
		if (binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\RedeemContainer.bin", FileMode.Create))
		{
			int redeemItemsCount = GetRedeemItemsCount();
			_ = binaryFile.WriteCount(ref redeemItemsCount);
			foreach (LegacyNativeConfiscatorRuntime value in PollContainers.Values)
			{
				foreach (LegacyNativeMsgDetainedItem value2 in value.RedeemContainer.Values)
				{
					LegacyNativeMsgDetainedItem msgDetainedItem = value2;
					_ = binaryFile.WriteDetained(ref msgDetainedItem);
				}
			}
			binaryFile.Close();
		}
		binaryFile = new LegacyNativeItemRecordFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\ClaimContainer.bin", FileMode.Create))
		{
			return;
		}
		int claimItemsCount = GetClaimItemsCount();
		_ = binaryFile.WriteCount(ref claimItemsCount);
		foreach (LegacyNativeConfiscatorRuntime value3 in PollContainers.Values)
		{
			foreach (LegacyNativeMsgDetainedItem value4 in value3.ClaimContainer.Values)
			{
				LegacyNativeMsgDetainedItem msgDetainedItem2 = value4;
				_ = binaryFile.WriteDetained(ref msgDetainedItem2);
			}
		}
		binaryFile.Close();
	}

internal unsafe void Load()
	{
		LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
		if (binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\RedeemContainer.bin", FileMode.Open))
		{
			int num = default(int);
			_ = binaryFile.ReadCount(ref num);
			LegacyNativeMsgDetainedItem value = default(LegacyNativeMsgDetainedItem);
			for (int i = 0; i < num; i++)
			{
				_ = binaryFile.ReadDetained(ref value);
				if (value.UID >= LegacyNativeConfiscatorRuntime.CounterUID.Count)
				{
					LegacyNativeConfiscatorRuntime.CounterUID.Set(value.UID + 1);
				}
				if (!PollContainers.ContainsKey(value.GainerUID))
				{
					PollContainers.TryAdd(value.GainerUID, new LegacyNativeConfiscatorRuntime());
				}
				if (!PollContainers.ContainsKey(value.OwnerUID))
				{
					PollContainers.TryAdd(value.OwnerUID, new LegacyNativeConfiscatorRuntime());
				}
				value.DaysLeft = (uint)(TimeSpan.FromTicks(DateTime.Now.Ticks).Days - TimeSpan.FromTicks(LegacyNativeConfiscatorRuntime.GetTimer(value.Date).Ticks).Days);
				if (value.Action == LegacyNativeMsgDetainedItem.ContainerType.RewardCps)
				{
					PollContainers[value.GainerUID].ClaimContainer.TryAdd(value.UID, value);
					continue;
				}
				if (value.DaysLeft > 7)
				{
					PollContainers[value.GainerUID].ClaimContainer.TryAdd(value.UID, value);
					continue;
				}
				PollContainers[value.GainerUID].ClaimContainer.TryAdd(value.UID, value);
				PollContainers[value.OwnerUID].RedeemContainer.TryAdd(value.UID, value);
			}
			binaryFile.Close();
		}
		binaryFile = new LegacyNativeItemRecordFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\ClaimContainer.bin", FileMode.Open))
		{
			return;
		}
		int num2 = default(int);
		_ = binaryFile.ReadCount(ref num2);
		LegacyNativeMsgDetainedItem value2 = default(LegacyNativeMsgDetainedItem);
		for (int j = 0; j < num2; j++)
		{
			_ = binaryFile.ReadDetained(ref value2);
			if (value2.UID >= LegacyNativeConfiscatorRuntime.CounterUID.Count)
			{
				LegacyNativeConfiscatorRuntime.CounterUID.Set(value2.UID + 1);
			}
			if (!PollContainers.ContainsKey(value2.GainerUID))
			{
				PollContainers.TryAdd(value2.GainerUID, new LegacyNativeConfiscatorRuntime());
			}
			if (!PollContainers.ContainsKey(value2.OwnerUID))
			{
				PollContainers.TryAdd(value2.OwnerUID, new LegacyNativeConfiscatorRuntime());
			}
			value2.DaysLeft = (uint)(TimeSpan.FromTicks(DateTime.Now.Ticks).Days - TimeSpan.FromTicks(LegacyNativeConfiscatorRuntime.GetTimer(value2.Date).Ticks).Days);
			PollContainers[value2.GainerUID].ClaimContainer.TryAdd(value2.UID, value2);
		}
		binaryFile.Close();
	}
}
